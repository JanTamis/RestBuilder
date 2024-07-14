using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using RestBuilder.Enumerators;
using RestBuilder.Helpers;
using RestBuilder.Interfaces;
using RestBuilder.Models;
using TypeShape.Roslyn;

namespace RestBuilder.Writers;

public static class CommentWriter
{
	public static void WriteComment(SourceWriter writer, ClassModel classModel, MethodModel methodModel)
	{
		WriteSummary(writer, classModel, methodModel);
		
		foreach (var parameter in methodModel.Parameters)
		{
			writerParameterComment(writer, parameter);
		}
	}
	
	public static void WriteSummary(SourceWriter writer, ClassModel classModel, MethodModel methodModel)
	{
		writer.WriteLine("/// <summary>");
		writer.WriteLine($"/// Sends a {methodModel.Method} request to <see href=\"{Path.Combine(classModel.BaseAddress, GetUrl(methodModel.Parameters, methodModel.Path))}\" />");
		writer.WriteLine("/// </summary>");
	}
	
	public static void writerParameterComment(SourceWriter writer, IType parameter)
	{
		if (parameter.IsType<CancellationToken>())
		{
			writer.WriteLine($"/// <param name=\"{parameter.Name}\">The {nameof(CancellationToken)} that is used for the request.</param>");
		}
		else
		{
			switch (parameter.Location.Location)
			{
				case HttpLocation.Query:
					writer.WriteLine($"/// <param name=\"{parameter.Name}\">Adds '{parameter.Location.Name ?? parameter.Name}={{{parameter.Name}}}' to the url.</param>");
					break;
				case HttpLocation.Header:
					writer.WriteLine($"/// <param name=\"{parameter.Name}\">The '{parameter.Location.Name ?? parameter.Name}' header of the request.</param>");
					break;
				case HttpLocation.Path:
					writer.WriteLine($"/// <param name=\"{parameter.Name}\">Fills the '{{{parameter.Location.Name ?? parameter.Name}}}' placeholder of the Url.</param>");
					break;
			}
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