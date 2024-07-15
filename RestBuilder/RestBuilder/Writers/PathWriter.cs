using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using RestBuilder.Enumerators;
using RestBuilder.Helpers;
using RestBuilder.Interfaces;
using RestBuilder.Models;
using RestBuilder.Parsers;
using TypeShape.Roslyn;

namespace RestBuilder.Writers;

public static class PathWriter
{
	private static readonly Regex HoleRegex = new Regex(@"\{\d+(:[^}]*)?\}");

	public static void WriteCreatePathMethod(MethodModel method, ImmutableEquatableArray<RequestQueryParamSerializerModel> queryParamSerializers, List<IType> optionalQueries, SourceWriter builder)
	{
		var hasQueries = method.Path.Contains("?");

		builder.WriteLine();

		using (builder.AppendIndentation("string CreatePath()"))
		{
			var hasVariable = !hasQueries 
			                  && (optionalQueries.Count > 1 
			                      || optionalQueries[0].Location.Location == HttpLocation.QueryMap 
			                      || optionalQueries.Any(a => a is ParameterModel { IsCollection: true })
			                      || optionalQueries.Any(a => GetQueryParamSerializer(queryParamSerializers, a) != null));

			builder.WriteLine($"DefaultInterpolatedStringHandler handler = $\"{GetPath(method.Path, method.Parameters, queryParamSerializers)}\";");

			if (hasVariable)
			{
				builder.WriteLine();
				builder.WriteLine("var hasQueries = false;");
			}

			for (var i = 0; i < optionalQueries.Count; i++)
			{
				WriteOptionalQuery(optionalQueries[i] as ParameterModel, queryParamSerializers, hasQueries, hasVariable, i == 0, optionalQueries.Count, builder);
			}

			builder.WriteLine();
			builder.WriteLine("return handler.ToStringAndClear();");
		}
	}
	
	public static void WriteOptionalQuery(ParameterModel query, ImmutableEquatableArray<RequestQueryParamSerializerModel> queryParamSerializers, bool hasQueries, bool hasVariable, bool isFirst, int queryCount, SourceWriter builder)
	{
		if (query.Location is { Location: HttpLocation.QueryMap } && query is { GenericTypes: { Length: 2 } genericTypes })
		{
			builder.WriteLine();

			if (query is { IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated })
			{
				builder.WriteLine($"foreach (var item in {query.Name} ?? Enumerable.Empty<KeyValuePair<{genericTypes[0].Type}, {genericTypes[1].Type}>>())");
			}
			else
			{
				builder.WriteLine($"foreach (var item in {query.Name})");
			}
			
			builder.WriteLine('{');

			builder.Indentation++;

			if (genericTypes[1] is { IsCollection: false, IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated })
			{
				AppendContinue("item.Value");
			}

			if (genericTypes[1].IsCollection)
			{
				if (genericTypes[1] is { IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated })
				{
					AppendContinue("item.Value");
				}

				if (query.Location.UrlEncode && NeedsUrlEncode(genericTypes[0].Namespace, genericTypes[0].Type))
				{
					builder.WriteLine($"var key = {ParseField("item.Key", genericTypes[0], query.Location)};");
					builder.WriteLine();
				}

				if (genericTypes[1].CollectionType is { IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated })
				{
					builder.WriteLine($"foreach (var queryValue in item.Value ?? Enumerable.Empty<{genericTypes[1].CollectionType.Type}>())");
				}
				else
				{
					builder.WriteLine("foreach (var queryValue in item.Value)");
				}

				
				builder.WriteLine('{');

				builder.Indentation++;

				if (genericTypes[1].CollectionType is { IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated })
				{
					AppendContinue("queryValue");
				}

				var querySerializer = GetQueryParamSerializer(queryParamSerializers, query);

				if (querySerializer is not null)
				{
					var keyName = query.Location.UrlEncode && NeedsUrlEncode(genericTypes[0].Namespace, genericTypes[0].Type)
						? "key"
						: "item.Key";
					
					using (builder.AppendIndentation($"foreach (var (name, value) in {querySerializer.Name}({keyName}, queryValue))"))
					{
						builder.WriteLine("handler.AppendLiteral(hasQueries ? \"&\" : \"?\");");
						builder.WriteLine("handler.AppendFormatted(name);");
						builder.WriteLine("handler.AppendLiteral(\"=\");");
						builder.WriteLine($"handler.AppendFormatted({BaseParseField("value", "System", "String", query.Location)});");
						builder.WriteLine();
						builder.WriteLine("hasQueries = true;");
					}
				}
				else if (query.Location.UrlEncode && NeedsUrlEncode(genericTypes[0].Namespace, genericTypes[0].Type))
				{
					AppendQueryItem("value", "key", genericTypes[1].CollectionType);
				}
				else
				{
					AppendQueryItem("value", String.Empty, genericTypes[1].CollectionType);
				}

				builder.Indentation--;

				builder.WriteLine('}');
			}
			else
			{
				var querySerializer = GetQueryParamSerializer(queryParamSerializers, query);

				if (querySerializer is not null)
				{
					AppendQuerySerializer(query, builder, querySerializer, "item.Key", "item.Value");
				}
				else
				{
					AppendQueryItem("item.Value", String.Empty, genericTypes[1]);
				}
			}

			builder.Indentation--;

			builder.WriteLine('}');

			return;
		}

		if (query.Location.Location is HttpLocation.Raw && query.IsNullable)
		{
			builder.WriteLine();

			if (query is { IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated })
			{
				builder.WriteLine($"if ({query.Name} != null)");
				builder.WriteLine('{');
			}

			if (hasQueries)
			{
				builder.WriteLine($"\thandler.AppendLiteral(\"&{query.Location.Name ?? query.Name}=\");");
			}
			else
			{
				if (!isFirst)
				{
					builder.WriteLine("\thandler.AppendLiteral(hasQueries ? \"&\" : \"?\");");
					builder.WriteLine($"\thandler.AppendLiteral(\"{query.Location.Name ?? query.Name}=\");");
				}
				else
				{
					builder.WriteLine($"\thandler.AppendLiteral(\"?{query.Location.Name ?? query.Name}=\");");
				}
			}

			builder.WriteLine($"handler.AppendFormatted({ParseFieldFormatted(query.Name, query.Namespace, query.Type, query.Location)});");

			if (hasVariable)
			{
				builder.WriteLine();
				builder.WriteLine("\thasQueries = true;");
			}

			if (query is { IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated })
			{
				builder.WriteLine('}');
			}

			return;
		}

		builder.WriteLine();

		if (query is { IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated })
		{
			builder.WriteLine($"if ({query.Name} != null)");
			builder.WriteLine('{');
			builder.Indentation++;
		}

		

		if (query.IsCollection)
		{
			var querySerializer = GetQueryParamSerializer(queryParamSerializers, query.CollectionItemType);
			
			builder.WriteLine($"foreach (var item in {query.Name})");
			builder.WriteLine('{');
			builder.Indentation++;

			if (query.CollectionItemType.IsNullable && query.CollectionItemType.NullableAnnotation == NullableAnnotation.Annotated)
			{
				AppendContinue("item");
			}

			if (querySerializer is not null)
			{
				AppendQuerySerializer(query, builder, querySerializer, $"\"{query.Location.Name ?? query.Name}\"", "item");
			}
			else
			{
				if (hasQueries)
				{
					builder.WriteLine($"handler.AppendLiteral(\"&{query.Location.Name ?? query.Name}=\");");
				}
				else
				{
					builder.WriteLine("handler.AppendLiteral(hasQueries ? \"&\" : \"?\");");
					builder.WriteLine($"handler.AppendLiteral(\"{query.Location.Name ?? query.Name}=\");");
				}

				builder.WriteLine($"handler.AppendFormatted({ParseFieldFormatted("item", query.CollectionItemType, query.Location)});");
			}

			builder.Indentation--;

			if (query is { IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated })
			{
				builder.WriteLine('}');
			}
		}
		else
		{
			var querySerializer = GetQueryParamSerializer(queryParamSerializers, query);
			
			if (querySerializer is not null)
			{
				AppendQuerySerializer(query, builder, querySerializer, $"\"{query.Location.Name ?? query.Name}\"", query.Name);
			}
			else
			{
				if (hasQueries)
				{
					builder.WriteLine($"handler.AppendLiteral(\"&{query.Location.Name ?? query.Name}=\");");
				}
				else
				{
					if (!isFirst)
					{
						builder.WriteLine("handler.AppendLiteral(hasQueries ? \"&\" : \"?\");");
						builder.WriteLine($"handler.AppendLiteral(\"{query.Location.Name ?? query.Name}=\");");
					}
					else
					{
						builder.WriteLine($"handler.AppendLiteral(\"?{query.Location.Name ?? query.Name}=\");");
					}
				}

				builder.WriteLine($"handler.AppendFormatted({ParseFieldFormatted(query.Name, query.Namespace, query.Type, query.Location)});");

				if (queryCount > 1)
				{
					builder.WriteLine();
					builder.WriteLine("hasQueries = true;");
				}
			}
		}
		
		

		if (query is { IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated })
		{
			builder.Indentation--;
			builder.WriteLine('}');
		}
		
		return;

		void AppendContinue(string fieldName)
		{
			using (builder.AppendIndentation($"if ({fieldName} is null)"))
			{
				builder.WriteLine("continue;");
			}

			builder.WriteLine();
		}

		void AppendQueryItem(string fieldItem, string keyName, TypeModel? type)
		{
			if (hasQueries)
			{
				builder.WriteLine("handler.AppendLiteral(\"&\");");
			}
			else
			{
				builder.WriteLine("handler.AppendLiteral(hasQueries ? \"&\" : \"?\");");
			}

			if (String.IsNullOrEmpty(keyName))
			{
				builder.WriteLine($"handler.AppendFormatted({ParseFieldFormatted("item.Key", genericTypes[0], query.Location)});");
			}
			else
			{
				builder.WriteLine($"handler.AppendFormatted({keyName});");
			}

			builder.WriteLine("handler.AppendLiteral(\"=\");");

			builder.WriteLine($"handler.AppendFormatted({ParseFieldFormatted(fieldItem, type, query.Location)});");

			if (!hasQueries)
			{
				builder.WriteLine();
				builder.WriteLine("hasQueries = true;");
			}
		}
	}
	
	private static void AppendQuerySerializer(ParameterModel query, SourceWriter builder, RequestQueryParamSerializerModel querySerializer, string queryKey, string queryValue)
	{
		using (builder.AppendIndentation($"foreach (var (name, value) in {querySerializer.Name}({queryKey}, {queryValue}))"))
		{
			builder.WriteLine("handler.AppendLiteral(hasQueries ? \"&\" : \"?\");");
			builder.WriteLine("handler.AppendFormatted(name);");
			builder.WriteLine("handler.AppendLiteral(\"=\");");
			builder.WriteLine($"handler.AppendFormatted({BaseParseField("value", "System", "String", query.Location)});");
			builder.WriteLine();
			builder.WriteLine("hasQueries = true;");
		}
	}

	public static string ParsePath(string path, IEnumerable<IType> parameters, ImmutableEquatableArray<RequestQueryParamSerializerModel> queryParamSerializers)
	{
		var hasHoles = parameters.Any(a => a.Location.Location is HttpLocation.Path or HttpLocation.Query or HttpLocation.QueryMap or HttpLocation.Raw);

		path = GetPath(path, parameters, queryParamSerializers);

		if (hasHoles)
		{
			if (parameters.Any(a => a.Location.Location == HttpLocation.Query
				    && a.IsNullable && a.NullableAnnotation == NullableAnnotation.Annotated || a.Location.Location == HttpLocation.QueryMap || (a.Location.Location == HttpLocation.Raw && a.IsNullable)
				    || GetQueryParamSerializer(queryParamSerializers, a) != null))
			{
				return $"CreatePath()";
			}

			return $"$\"{path}\"";
		}

		return $"\"{path}\"";
	}

	private static string GetPath(string path, IEnumerable<IType> parameters, ImmutableEquatableArray<RequestQueryParamSerializerModel> queryParamSerializers)
	{
		var hasHoles = parameters.Any(a => a.Location.Location is HttpLocation.Path or HttpLocation.Query or HttpLocation.Raw);

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
					resultPath.Append($"{{{parameter.Name}}}");
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

		path = AddQueryString(path, parameters
			.Where(w => w.Location.Location == HttpLocation.Query 
			            && (!w.IsNullable || w.NullableAnnotation == NullableAnnotation.NotAnnotated) 
			            && GetQueryParamSerializer(queryParamSerializers, w) == null)
			.Select(s => new KeyValuePair<string, string>(s.Location.Name ?? s.Name, "{" + ParseFieldInline(s.Name, s.Namespace, s.Type, s.Location) + "}")));

		var hasQuery = path.Contains('?');

		foreach (var parameter in parameters.Where(w => w.Location.Location == HttpLocation.Raw && !w.IsNullable))
		{
			path += hasQuery ? "&" : "?";
			path += '{' + parameter.Name + '}';
			hasQuery = true;
		}

		return path;
	}

	public static string AddQueryString(string uri, IEnumerable<KeyValuePair<string, string?>> queryString)
	{
		var anchorIndex = uri.IndexOf('#');
		var uriToBeAppended = uri.AsSpan();
		var anchorText = ReadOnlySpan<char>.Empty;
		// If there is an anchor, then the query string must be inserted before its first occurrence.
		if (anchorIndex != -1)
		{
			anchorText = uriToBeAppended.Slice(anchorIndex);
			uriToBeAppended = uriToBeAppended.Slice(0, anchorIndex);
		}

		var hasQuery = uriToBeAppended.Contains(['?'], StringComparison.InvariantCulture);

		var sb = new StringBuilder();
		sb.Append(uriToBeAppended.ToString());

		foreach (var parameter in queryString)
		{
			if (parameter.Value == null)
			{
				continue;
			}

			sb.Append(hasQuery ? '&' : '?');
			sb.Append(Uri.EscapeDataString(parameter.Key));
			sb.Append('=');
			sb.Append(parameter.Value);

			hasQuery = true;
		}

		sb.Append(anchorText.ToString());
		return sb.ToString();
	}

	private static string ParseField(string fieldName, TypeModel type, LocationAttributeModel location)
	{
		return ParseField(fieldName, type.Namespace, type.Name, location);
	}

	private static string ParseFieldFormatted(string fieldName, TypeModel type, LocationAttributeModel location)
	{
		return ParseFieldFormatted(fieldName, type.Namespace, type.Name, location);
	}

	private static string ParseField(string fieldName, string @namespace, string type, LocationAttributeModel location)
	{
		var result = BaseParseField(fieldName, @namespace, type, location);

		if (String.IsNullOrEmpty(result))
		{
			if (!String.IsNullOrEmpty(location.Format))
			{
				result = $"{fieldName}.ToString(\"{location.Format}\")";
			}
			else
			{
				result = fieldName;
			}
		}

		return result;
	}

	private static string ParseFieldInline(string fieldName, string @namespace, string type, LocationAttributeModel location)
	{
		var result = BaseParseField(fieldName, @namespace, type, location);

		if (String.IsNullOrEmpty(result))
		{
			if (!String.IsNullOrEmpty(location.Format))
			{
				result = $"{fieldName}:{location.Format}";
			}
			else
			{
				result = fieldName;
			}
		}

		return result;
	}

	private static string ParseFieldFormatted(string fieldName, string @namespace, string type, LocationAttributeModel location)
	{
		var result = BaseParseField(fieldName, @namespace, type, location);

		if (String.IsNullOrEmpty(result))
		{
			if (!String.IsNullOrEmpty(location.Format))
			{
				result = $"{fieldName}, \"{location.Format}\"";
			}
			else
			{
				result = fieldName;
			}
		}

		return result;
	}

	private static string BaseParseField(string fieldName, string @namespace, string type, LocationAttributeModel location)
	{
		if (@namespace == "System" && type is "String" or "string")
		{
			if (location.UrlEncode)
			{
				return $"Uri.EscapeDataString({fieldName})";
			}

			return fieldName;
		}

		if (HoleRegex.IsMatch(location.Format ?? String.Empty))
		{
			var result = $"$\"{FillHoles(location.Format, fieldName)}\"";

			if (location.UrlEncode)
			{
				result = $"Uri.EscapeDataString({result})";
			}

			return result;
		}

		if (location.UrlEncode && NeedsUrlEncode(@namespace, type))
		{
			var format = !String.IsNullOrEmpty(location.Format)
				? $"\"{location.Format}\""
				: String.Empty;

			return $"Uri.EscapeDataString({fieldName}.ToString({format}))";
		}

		if (!String.IsNullOrEmpty(location.Format) && location.UrlEncode)
		{
			return $"Uri.EscapeDataString({fieldName}.ToString(\"{location.Format}\"))";
		}

		return String.Empty;
	}

	private static bool NeedsUrlEncode(string @namespace, string type)
	{
		return !(@namespace is "System" &&
		         type is "Int16" or "short" or "Int32" or "int" or "Int64" or "long" or "UInt16" or "ushort" or "UInt32" or "uint" or "UInt64" or "ulong" or "Single" or "Double" or "Decimal" or "Boolean" or "Char");
	}

	private static string FillHoles(string value, params string[] fieldName)
	{
		return HoleRegex.Replace(value, m =>
		{
			var result = m.Value;

			for (var i = 0; i < fieldName.Length; i++)
			{
				result = m.Value.Replace(i.ToString(), fieldName[i]);
			}

			if (result == m.Value)
			{
				result = result
					.Replace("{", "{{")
					.Replace("}", "}}");
			}

			return result;
		});
	}

	public static RequestQueryParamSerializerModel? GetQueryParamSerializer(ImmutableEquatableArray<RequestQueryParamSerializerModel> queryParamSerializers, IType? query)
	{
		var result = queryParamSerializers
			.Where(w => !w.ValueType.IsGeneric && ClassParser.TypeEquals(w.ValueType, query))
			.DefaultIfEmpty(queryParamSerializers
				.FirstOrDefault(w => w.ValueType.IsGeneric))
			.First();
		
		Debug.WriteLine(result);
		
		return result;
	}
}