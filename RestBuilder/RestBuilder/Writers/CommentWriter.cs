using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using RestBuilder.Enumerators;
using RestBuilder.Helpers;
using RestBuilder.Interfaces;
using RestBuilder.Models;
using RestBuilder.Parsers;
using TypeShape.Roslyn;

namespace RestBuilder.Writers;

public static class CommentWriter
{
	public static void WriteComment(SourceWriter writer, ClassModel classModel, MethodModel methodModel)
	{
		WriteSummary(writer, classModel, methodModel);

		if (methodModel.ReturnNamespace != "System" && methodModel.ReturnTypeName != "void")
		{
			var bodySerializer = classModel.ResponseDeserializers.FirstOrDefault(a => ClassParser.TypeEquals(methodModel.ReturnType, a.Type));

			if (bodySerializer != null)
			{
				writer.WriteLine($"/// <returns>Processes the response via {bodySerializer.Name} and returns the result.</returns>");
			}
			else if (methodModel.ReturnType.IsType<string>())
			{
				writer.WriteLine($"/// <returns>Returns the content of the response as a string.</returns>");
			}
			else if (methodModel.ReturnType.IsType<byte[]>())
			{
				writer.WriteLine($"/// <returns>Returns the content as a byte array.</returns>");
			}
			else if (methodModel.ReturnType.IsType<Stream>())
			{
				writer.WriteLine($"/// <returns>Returns the content as a Stream.</returns>");
			}
			else
			{
				writer.WriteLine($"/// <returns>Reads the content of the response as json and parses it as {methodModel.ReturnTypeName}.</returns>");
			}
		}

		foreach (var parameter in methodModel.Parameters)
		{
			writerParameterComment(writer, parameter, classModel);
		}
		
		var parametersThatThrow = methodModel.Parameters
			.Where(w => w.IsNullable && w.NullableAnnotation == NullableAnnotation.NotAnnotated)
			.ToList();

		if (parametersThatThrow.Any())
		{
			writer.WriteLine("/// <exception cref=\"ArgumentNullException\">");
			//writer.WriteLine($"/// Throws if {String.Join(", ", parametersThatThrow.Select(s => $"<see cref=\"{s.Name}\" />"))} is null.");

			var result = String.Empty;
			
			for (var i = 0; i < parametersThatThrow.Count; i++)
			{
				if (i == 0)
				{
					result += $"<see cref=\"{parametersThatThrow[i].Name}\" />";
				}
				else if (i < parametersThatThrow.Count - 1)
				{
					result += $", <see cref=\"{parametersThatThrow[i].Name}\" />";
				}
				
				else
				{
					result += $" or <see cref=\"{parametersThatThrow[i].Name}\" />";
				}
			}
			
			writer.WriteLine($"/// Throws if {result} is null.");

			writer.WriteLine("/// </exception>");
		}

		if (!methodModel.AllowAnyStatusCode)
		{
			writer.WriteLine("/// <exception cref=\"HttpRequestException\">");
			writer.WriteLine("/// Throws if the request failed or the response status code is not a success code.");
			writer.WriteLine("/// </exception>");
		}
	}

	public static void WriteSummary(SourceWriter writer, ClassModel classModel, MethodModel methodModel)
	{
		writer.WriteLine("/// <summary>");
		writer.WriteLine($"/// Sends a {methodModel.Method} request to <see href=\"{Path.Combine(classModel.BaseAddress, GetUrl(methodModel.Parameters, methodModel.Path))}\" />");
		writer.WriteLine("/// </summary>");
	}

	public static void writerParameterComment(SourceWriter writer, IType parameter, ClassModel classModel)
	{
		if (parameter.IsType<CancellationToken>())
		{
			writer.WriteLine($"/// <param name=\"{parameter.Name}\">The {nameof(CancellationToken)} that is used for the request.</param>");
		}
		else
		{
			var result = $"/// <param name=\"{parameter.Name}\">";

			switch (parameter.Location.Location)
			{
				case HttpLocation.Query:
					var queryParser = PathWriter.GetQueryParamSerializer(classModel.RequestQueryParamSerializers, parameter);

					if (queryParser != null)
					{
						result += $"Invokes {queryParser.Name}('{parameter.Location.Name ?? parameter.Name}', <see cref=\"{parameter.Name}\" />) and appends the query result to the url.";
					}
					else
					{
						result += $"Appends '{parameter.Location.Name ?? parameter.Name}={{{parameter.Name}}}' to the url.";
					}

					break;
				case HttpLocation.Header:
					result += $"Sets the '{parameter.Location.Name ?? parameter.Name}' header of the request.";
					break;
				case HttpLocation.Path:
					result += $"Fills the '{{{parameter.Location.Name ?? parameter.Name}}}' placeholder of the Url.";
					break;
				case HttpLocation.Body:
					var bodySerializer = classModel.RequestBodySerializers.FirstOrDefault(a => ClassParser.TypeEquals(parameter, a.Type));
					
					if (bodySerializer != null)
					{
						result += $"Invokes {bodySerializer.Name}(<see cref=\"{parameter.Name}\" />) and assigns the result to the body of the request.";
					}
					else
					{
						if (parameter.IsType<string>())
						{
							result += $"Sets the body to 'new StringContent(<see cref=\"{parameter.Name}\" />)'.";
						}
						else if (parameter.IsType<byte[]>())
						{
							result += $"Sets the body to 'new ByteArrayContent(<see cref=\"{parameter.Name}\" />)'.";
						}
						else if (parameter.IsType<Stream>())
						{
							result += $"Sets the body to 'new StreamContent(<see cref=\"{parameter.Name}\" />)'.";
						}
						else if (parameter.IsType<HttpContent>())
						{
							result += $"Sets the body to <see cref=\"{parameter.Name}\" />.";
						}
						else
						{
							result += $"Sets the body to 'JsonContent.Create(<see cref=\"{parameter.Name}\" />)'"; 
						}
					}
					
					break;
				case HttpLocation.Raw:
					result += $"The raw query value of the request.";
					break;
				case HttpLocation.QueryMap:
					result += $"The query map value of the request.";
					break;
			}

			writer.WriteLine(result + "</param>");
		}
	}

	private static string GetUrl(IEnumerable<IType> parameters, string path)
	{
		var hasHoles = parameters.Any(a => a.Location.Location is HttpLocation.Path
			or HttpLocation.Query
			or HttpLocation.Raw);

		if (hasHoles)
		{
			var pathHoles = parameters
				.Where(w => w.Location.Location == HttpLocation.Path)
				.ToDictionary(t => t.Location.Name ?? t.Name, t => t);

			var index = 0;
			var resultPath = new StringBuilder();

			var matches = Regex.Matches(path, @"\{[^\{-\}]*\}");

			foreach (Match match in matches)
			{
				resultPath.Append(path
					.Substring(index, match.Index - index)
					.Replace("{", "%7B")
					.Replace("}", "%7D"));

				if (pathHoles.TryGetValue(match.Value.Substring(1, match.Value.Length - 2), out var parameter))
				{
					resultPath.Append($"{{{parameter.Location.Name ?? parameter.Name}}}");
				}
				else
				{
					resultPath.Append(match.Value
						.Replace("{", "%7B")
						.Replace("}", "%7D"));
				}

				index = match.Index + match.Length;
			}

			resultPath.Append(path[index..]
				.Replace("{", "%7B")
				.Replace("}", "%7D"));

			path = resultPath.ToString();
		}

		return path;
	}
}