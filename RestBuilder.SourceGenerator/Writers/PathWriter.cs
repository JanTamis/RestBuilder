using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using RestBuilder.SourceGenerator.Enumerators;
using RestBuilder.SourceGenerator.Helpers;
using RestBuilder.SourceGenerator.Interfaces;
using RestBuilder.SourceGenerator.Models;
using RestBuilder.SourceGenerator.Parsers;
using TypeShape.Roslyn;

namespace RestBuilder.SourceGenerator.Writers;

public static class PathWriter
{
	private static readonly Regex HoleRegex = new Regex(@"\{\d+(:[^}]*)?\}");

	public static void WritePath(MethodModel method, ImmutableEquatableArray<RequestQueryParamSerializerModel> queryParamSerializers, List<ParameterModel> optionalQueries, SourceWriter builder)
	{
		var path = GetPath(method.Path, method.Parameters, queryParamSerializers);

		builder.WriteLine("// Create the url");
		builder.WriteLine($"var builder = new UrlBuilder($\"{path}\");");

		AppendQueries(queryParamSerializers, optionalQueries, builder);
		AppendQueryMaps(queryParamSerializers, optionalQueries, builder);

		builder.WriteLine();
	}

	private static void AppendQueryMaps(ImmutableEquatableArray<RequestQueryParamSerializerModel> queryParamSerializers, List<ParameterModel> optionalQueries, SourceWriter builder)
	{
		var queryMaps = optionalQueries
			.Where(w => w.Location.Location == HttpLocation.QueryMap)
			.Select(s => new
			{
				Type = s,
				QuerySerializer = GetQueryParamSerializer(queryParamSerializers, s.GenericTypes[1]),
			})
			.OrderBy(o => o.QuerySerializer is null);

		foreach (var queryMap in queryMaps)
		{
			var querySerializer = queryMap.QuerySerializer;
			var type = queryMap.Type;

			var encode = type.Location.UrlEncode && NeedsUrlEncode(type.Namespace, type.Type)
				? String.Empty
				: ", false";

			if (querySerializer != null)
			{
				builder.WriteLine();

				using (builder.AppendIndentation($"foreach (var query in {type.Name})"))
				{
					using (builder.AppendIndentationWithCondition("foreach (var queryItem in query.Value)", () => type.GenericTypes[1].IsCollection))
					{
						var queryName = type.GenericTypes[1].IsCollection ? "queryItem" : "query";
						
						if (type.GenericTypes[1] is { IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated, IsCollection: true })
						{
							AppendContinue(builder, "queryItem");
						}
					
						using (builder.AppendIndentation($"foreach (var item in {querySerializer.Name}(query.Key, {queryName}))"))
						{
							builder.WriteLine($"builder.AppendQuery(item.Key, item.Value{encode});");
						}
					}
				}
			}
			else
			{
				if (type.IsCollection)
				{
					builder.WriteLine();

					using (builder.AppendIndentation($"foreach (var query in {type.Name})"))
					{
						if (type is { IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated })
						{
							AppendContinue(builder, "query");
						}

						using (builder.AppendIndentation("foreach (var item in query.Value)"))
						{
							if (type.GenericTypes[1] is { IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated })
							{
								AppendContinue(builder, "item");
							}

							builder.WriteLine($"builder.AppendQuery(query.Key, item{encode});");
						}
					}
				}
				else
				{
					using (builder.AppendIndentation($"foreach (var item in {type.Name})"))
					{
						if (type is { IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated })
						{
							AppendContinue(builder, "item");
						}

						builder.WriteLine($"builder.AppendQuery(item.Key, item.Value{encode});");
					}
				}
			}
		}
	}

	private static void AppendQueries(ImmutableEquatableArray<RequestQueryParamSerializerModel> queryParamSerializers, List<ParameterModel> optionalQueries, SourceWriter builder)
	{
		var queries = optionalQueries
			.Where(w => w.Location.Location == HttpLocation.Query)
			.Select(s => new
			{
				Type = s,
				QuerySerializer = GetQueryParamSerializer(queryParamSerializers, s.IsCollection
					? s.CollectionItemType
					: s),
			})
			.OrderBy(o => o.QuerySerializer is null)
			.ThenBy(t => t.Type.IsCollection);

		foreach (var query in queries)
		{
			var querySerializer = query.QuerySerializer;
			var type = query.Type;

			var encode = type.Location.UrlEncode && NeedsUrlEncode(type.Namespace, type.Type)
				? String.Empty
				: ", false";

			if (querySerializer != null)
			{
				builder.WriteLine();
				
				var queryName = type.IsCollection ? "query" : type.Name;

				using (builder.AppendIndentationWithCondition($"foreach (var query in {type.Name})", () => type.IsCollection))
				{
					using (builder.AppendIndentation($"foreach (var item in {querySerializer.Name}(\"{type.Location.Name ?? type.Name}\", {queryName}))"))
					{
						builder.WriteLine($"builder.AppendQuery(item.Key, item.Value{encode});");
					}
				}
			}
			else
			{
				if (type.IsCollection)
				{
					builder.WriteLine();

					using (builder.AppendIndentation($"foreach (var query in {type.Name})"))
					{
						if (type is { IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated })
						{
							AppendContinue(builder, "query");
						}

						builder.WriteLine($"builder.AppendQuery(\"{type.Location.Name ?? type.Name}\", query{encode});");
					}
				}
				else
				{
					using (builder.AppendnullCheck(type))
					{
						builder.WriteLine($"builder.AppendQuery(\"{type.Location.Name ?? type.Name}\", {type.Name}{encode});");
					}
				}
			}
		}
	}

	// public static void WriteCreatePathMethod(MethodModel method, ImmutableEquatableArray<RequestQueryParamSerializerModel> queryParamSerializers, List<IType> optionalQueries, SourceWriter builder)
	// {
	// 	var hasQueries = method.Path.Contains("?");
	//
	// 	builder.WriteLine();
	//
	// 	using (builder.AppendIndentation("string CreatePath()"))
	// 	{
	// 		
	//
	// 		var hasVariable = (optionalQueries.Count > 1
	// 		                   || optionalQueries[0].Location.Location == HttpLocation.QueryMap
	// 		                   || optionalQueries.Any(a => a is ParameterModel { IsCollection: true })
	// 		                   || optionalQueries.Any(a => GetQueryParamSerializer(queryParamSerializers, a) != null))
	// 		                  && !path.Contains('?');
	//
	// 		builder.WriteLine($"DefaultInterpolatedStringHandler handler = $\"{path}\";");
	//
	// 		if (hasVariable)
	// 		{
	// 			builder.WriteLine();
	// 			builder.WriteLine("var hasQueries = false;");
	// 		}
	//
	// 		for (var i = 0; i < optionalQueries.Count; i++)
	// 		{
	// 			WriteOptionalQuery(optionalQueries[i] as ParameterModel, queryParamSerializers, hasQueries, hasVariable, i == 0, optionalQueries.Count, builder);
	// 		}
	//
	// 		builder.WriteLine();
	// 		builder.WriteLine("return handler.ToStringAndClear();");
	// 	}
	// }

	public static void WriteOptionalQuery(ParameterModel query, ImmutableEquatableArray<RequestQueryParamSerializerModel> queryParamSerializers, bool hasQueries, bool hasVariable, bool isFirst, int queryCount, SourceWriter builder)
	{
		if (query.Location is { Location: HttpLocation.QueryMap } && query is { GenericTypes: { Length: 2 } genericTypes })
		{
			// AppendQueryMap(query, queryParamSerializers, hasQueries, hasVariable, builder, genericTypes);
		}
		else if (query.Location.Location is HttpLocation.Raw && query.IsNullable)
		{
			AppendRawQuery(query, hasQueries, hasVariable, isFirst, builder);
		}
		else
		{
			builder.WriteLine();

			if (query is { IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated })
			{
				builder.WriteLine($"if ({query.Name} != null)");
				builder.WriteLine('{');
				builder.Indentation++;
			}

			if (query.IsCollection)
			{
				AppendQueryList(query, queryParamSerializers, hasQueries, hasVariable, builder);
			}
			else
			{
				AppendQuery(query, queryParamSerializers, hasQueries, hasVariable, isFirst, builder);
			}

			if (query is { IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated })
			{
				builder.Indentation--;
				builder.WriteLine('}');
			}
		}
	}

	private static void AppendQuery(ParameterModel query, ImmutableEquatableArray<RequestQueryParamSerializerModel> queryParamSerializers, bool hasQueries, bool hasVariable, bool isFirst, SourceWriter builder)
	{
		var querySerializer = GetQueryParamSerializer(queryParamSerializers, query);

		if (querySerializer is not null)
		{
			AppendQuerySerializer(query, builder, querySerializer, $"\"{query.Location.Name ?? query.Name}\"", query.Name, hasQueries, hasVariable);
		}
		else
		{
			if (hasVariable)
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

			if (hasVariable)
			{
				builder.WriteLine();
				builder.WriteLine("hasQueries = true;");
			}
		}
	}

	private static void AppendQueryList(ParameterModel query, ImmutableEquatableArray<RequestQueryParamSerializerModel> queryParamSerializers, bool hasQueries, bool hasVariable, SourceWriter builder)
	{
		var querySerializer = GetQueryParamSerializer(queryParamSerializers, query.CollectionItemType);

		builder.WriteLine($"foreach (var item in {query.Name})");
		builder.WriteLine('{');
		builder.Indentation++;

		if (query.CollectionItemType.IsNullable && query.CollectionItemType.NullableAnnotation == NullableAnnotation.Annotated)
		{
			AppendContinue(builder, "item");
		}

		if (querySerializer is not null)
		{
			AppendQuerySerializer(query, builder, querySerializer, $"\"{query.Location.Name ?? query.Name}\"", "item", hasQueries, hasVariable);
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

	private static void AppendRawQuery(ParameterModel query, bool hasQueries, bool hasVariable, bool isFirst, SourceWriter builder)
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

		if (!hasVariable)
		{
			builder.WriteLine();
			builder.WriteLine("\thasQueries = true;");
		}

		if (query is { IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated })
		{
			builder.WriteLine('}');
		}
	}

	// private static void AppendQueryMap(ParameterModel query, ImmutableEquatableArray<RequestQueryParamSerializerModel> queryParamSerializers, bool hasQueries, bool hasVariable, SourceWriter builder, ImmutableEquatableArray<TypeModel> genericTypes)
	// {
	// 	builder.WriteLine();
	//
	// 	if (query is { IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated })
	// 	{
	// 		builder.WriteLine($"foreach (var item in {query.Name} ?? Enumerable.Empty<KeyValuePair<{genericTypes[0].Type}, {genericTypes[1].Type}>>())");
	// 	}
	// 	else
	// 	{
	// 		builder.WriteLine($"foreach (var item in {query.Name})");
	// 	}
	//
	// 	builder.WriteLine('{');
	//
	// 	builder.Indentation++;
	//
	// 	if (genericTypes[1] is { IsCollection: false, IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated })
	// 	{
	// 		AppendContinue(builder, "item.Value");
	// 	}
	//
	// 	if (genericTypes[1].IsCollection)
	// 	{
	// 		if (genericTypes[1] is { IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated })
	// 		{
	// 			AppendContinue(builder, "item.Value");
	// 		}
	//
	// 		if (query.Location.UrlEncode && NeedsUrlEncode(genericTypes[0].Namespace, genericTypes[0].Type))
	// 		{
	// 			builder.WriteLine($"var key = {ParseField("item.Key", genericTypes[0], query.Location)};");
	// 			builder.WriteLine();
	// 		}
	//
	// 		if (genericTypes[1].CollectionType is { IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated })
	// 		{
	// 			builder.WriteLine($"foreach (var queryValue in item.Value ?? Enumerable.Empty<{genericTypes[1].CollectionType.Type}>())");
	// 		}
	// 		else
	// 		{
	// 			builder.WriteLine("foreach (var queryValue in item.Value)");
	// 		}
	//
	// 		builder.WriteLine('{');
	//
	// 		builder.Indentation++;
	//
	// 		if (genericTypes[1].CollectionType is { IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated })
	// 		{
	// 			AppendContinue(builder, "queryValue");
	// 		}
	//
	// 		var querySerializer = GetQueryParamSerializer(queryParamSerializers, query);
	//
	// 		if (querySerializer is not null)
	// 		{
	// 			var keyName = query.Location.UrlEncode && NeedsUrlEncode(genericTypes[0].Namespace, genericTypes[0].Type)
	// 				? "key"
	// 				: "item.Key";
	//
	// 			using (builder.AppendIndentation($"foreach (var (name, value) in {querySerializer.Name}({keyName}, queryValue))"))
	// 			{
	// 				if (query.Location.UrlEncode && NeedsUrlEncode(query.Namespace, query.Type))
	// 				{
	// 					builder.WriteLine("builder.AppendQuery(name, value);");
	// 				}
	// 				else
	// 				{
	// 					builder.WriteLine("builder.AppendQuery(name, value, false);");
	// 				}
	// 			}
	// 		}
	// 		else if (query.Location.UrlEncode && NeedsUrlEncode(genericTypes[0].Namespace, genericTypes[0].Type))
	// 		{
	// 			AppendQueryItem("value", "key", genericTypes[1].CollectionType);
	// 		}
	// 		else
	// 		{
	// 			AppendQueryItem("value", String.Empty, genericTypes[1].CollectionType);
	// 		}
	//
	// 		builder.Indentation--;
	//
	// 		builder.WriteLine('}');
	// 	}
	// 	else
	// 	{
	// 		var querySerializer = GetQueryParamSerializer(queryParamSerializers, query);
	//
	// 		if (querySerializer is not null)
	// 		{
	// 			AppendQuerySerializer(query, builder, querySerializer, "item.Key", "item.Value", hasQueries, hasVariable);
	// 		}
	// 		else
	// 		{
	// 			AppendQueryItem("item.Value", String.Empty, genericTypes[1]);
	// 		}
	// 	}
	//
	// 	builder.Indentation--;
	//
	// 	builder.WriteLine('}');
	// }

	private static void AppendQuerySerializer(ParameterModel query, SourceWriter builder, RequestQueryParamSerializerModel querySerializer, string queryKey, string queryValue, bool hasQueries, bool hasVariable)
	{
		using (builder.AppendIndentation($"foreach (var (name, value) in {querySerializer.Name}({queryKey}, {queryValue}))"))
		{
			if (query.Location.UrlEncode && NeedsUrlEncode(query.Namespace, query.Type))
			{
				builder.WriteLine("builder.AppendQuery(name, value);");
			}
			else
			{
				builder.WriteLine("builder.AppendQuery(name, value, false);");
			}
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
				return $"builder.ToUriAndClear()";
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
		return queryParamSerializers
			.Where(w => !w.ValueType.IsGeneric && ClassParser.TypeEquals(w.ValueType, query))
			.DefaultIfEmpty(queryParamSerializers
				.FirstOrDefault(w => w.ValueType.IsGeneric))
			.First();
	}

	private static void AppendContinue(SourceWriter builder, string fieldName)
	{
		using (builder.AppendIndentation($"if ({fieldName} is null)"))
		{
			builder.WriteLine("continue;");
		}

		builder.WriteLine();
	}
}