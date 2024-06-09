using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using RestBuilder.Enumerators;
using RestBuilder.Helpers;
using RestBuilder.Interfaces;
using RestBuilder.Models;
using RestBuilder.Parsers;
using TypeShape.Roslyn;

namespace RestBuilder;

[Generator]
public class RestSourceSourceGenerator : IIncrementalGenerator
{
	private static readonly Regex HoleRegex = new Regex(@"\{\d+(:[^}]*)?\}");

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Add the marker attribute to the compilation.
		context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
			$"{Literals.BaseAddressAttribute}.g.cs",
			SourceText.From(Literals.AttributeSourceCode, Encoding.UTF8)));

		context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
			"RequestAttributes.g.cs",
			SourceText.From(Literals.RequestAttributes, Encoding.UTF8)));

		context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
			"QueryAttributes.g.cs",
			SourceText.From(Literals.QueryAttribute, Encoding.UTF8)));

		context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
			"QuerySerializationMethod.g.cs",
			SourceText.From(Literals.QuerySerializationMethod, Encoding.UTF8)));

		context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
			"AllowAnyStatusCodeAttribute.g.cs",
			SourceText.From(Literals.AllowAnyStatusCodeAttribute, Encoding.UTF8)));

		context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
			"HeaderAttribute.g.cs",
			SourceText.From(Literals.HeaderAttribute, Encoding.UTF8)));

		context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
			"PathAttribute.g.cs",
			SourceText.From(Literals.PathAttribute, Encoding.UTF8)));

		context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
			"BodyAttribute.g.cs",
			SourceText.From(Literals.BodyAttribute, Encoding.UTF8)));

		context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
			"RestClientAttribute.g.cs",
			SourceText.From(Literals.RestClientAttribute, Encoding.UTF8)));

		context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
			"QueryMapAttribute.g.cs",
			SourceText.From(Literals.QueryMapAttribute, Encoding.UTF8)));

		var classes = context.SyntaxProvider
			.ForAttributeWithMetadataName(
				$"{Literals.BaseNamespace}.{nameof(Literals.RestClientAttribute)}",
				(node, token) => node is ClassDeclarationSyntax classDeclaration && classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)),
				GenerateSource);

		context.RegisterSourceOutput(classes,
			static (spc, source) => Execute(source, spc));
	}

	private static ClassModel GenerateSource(GeneratorAttributeSyntaxContext context, CancellationToken token)
	{
		return ClassParser.Parse(context.TargetNode as ClassDeclarationSyntax, context.TargetSymbol as INamedTypeSymbol, context.Attributes, context.SemanticModel.Compilation);
	}

	private static void Execute(ClassModel source, SourceProductionContext context)
	{
		var builder = new SourceWriter('\t', 1);

		WriteNamespaces(source, builder);
		WriteClassStart(source, builder);
		WriteMethods(source, builder);
		WriteClassEnd(builder);

		context.AddSource($"{source.Name}.g.cs", builder.ToSourceText());
	}

	private static void WriteNamespaces(ClassModel source, SourceWriter builder)
	{
		var namespaces = source.Methods
			.SelectMany(s => s.Parameters)
			.Select(s => s.Namespace)
			.Concat(source.Methods.Select(s => s.ReturnNamespace))
			.Concat(
			[
				"System",
				"System.Net.Http",
				"System.Net.Http.Json",
				"System.Threading",
				"System.Threading.Tasks",
				"System.Runtime.CompilerServices"
			])
			.Where(w => !String.IsNullOrEmpty(w))
			.Distinct()
			.OrderBy(o => o)
			.Select(s => $"using {s};");

		foreach (var @namespace in namespaces)
		{
			builder.WriteLine(@namespace);
		}
	}

	private static void WriteClassStart(ClassModel source, SourceWriter builder)
	{
		var hasHeaders = source.Attributes.Any(a => a.Location == HttpLocation.Header);

		builder.WriteLine($$"""
				
			namespace {{source.Namespace}};
			
			public partial class {{source.Name}}
			{
			""");

		builder.Indentation = 1;

		if (!hasHeaders && String.IsNullOrEmpty(source.BaseAddress))
		{
			builder.WriteLine($"public HttpClient {source.ClientName} {{ get; }} = new HttpClient();");
		}
		else
		{
			builder.WriteLine($$"""
				public HttpClient {{source.ClientName}} { get; } = new HttpClient() 
				{
				""");
		}

		if (!String.IsNullOrEmpty(source.BaseAddress))
		{
			builder.WriteLine($"\tBaseAddress = new Uri(\"{source.BaseAddress}\"),");
		}

		if (hasHeaders)
		{
			WriteDefaultRequestHeaders(source, builder);
		}

		if (hasHeaders || !String.IsNullOrEmpty(source.BaseAddress))
		{
			builder.WriteLine("\t};");
		}

		builder.Indentation = 0;
	}

	private static void WriteDefaultRequestHeaders(ClassModel source, SourceWriter builder)
	{
		builder.Indentation = 2;

		builder.WriteLine("DefaultRequestHeaders = ");
		builder.WriteLine('{');

		foreach (var header in source.Attributes.Where(w => w.Location == HttpLocation.Header))
		{
			builder.WriteLine($"\t{{ \"{header.Name}\", \"{header.Value}\" }},");
		}

		builder.WriteLine('}');

		builder.Indentation = 0;
	}

	private static void WriteMethods(ClassModel source, SourceWriter builder)
	{
		foreach (var method in source.Methods)
		{
			WriteMethod(method, source, builder);
		}
	}

	private static void WriteMethod(MethodModel method, ClassModel source, SourceWriter builder)
	{
		var returnType = "Task";

		var tokenText = method.Parameters
			.Concat<IType>(source.Properties.Where(w => w.Location.Location == HttpLocation.Header))
			.DistinctBy(d => d.Location.Name ?? d.Name)
			.Where(w => w.Namespace == "System.Threading" && w.Type is nameof(CancellationToken))
			.Select(s => s.Name)
			.DefaultIfEmpty("CancellationToken.None")
			.First();

		if (!String.IsNullOrEmpty(method.ReturnType))
		{
			returnType = $"Task<{method.ReturnType}>";
		}

		builder.WriteLine();

		builder.Indentation++;

		builder.WriteLine($"public partial async {returnType} {method.Name}({String.Join(", ", method.Parameters.Select(s => $"{s.Type} {s.Name}"))})");
		builder.WriteLine('{');
		builder.Indentation++;

		var items = method.Parameters
			.Concat<IType>(source.Properties);

		WriteMethodBody(method, source.ClientName, items, tokenText, builder);

		builder.Indentation--;
		builder.WriteLine('}');
	}

	private static void WriteMethodBody(MethodModel method, string clientName, IEnumerable<IType> items, string tokenText, SourceWriter builder)
	{
		var headers = items
			.Where(w => w.Location.Location == HttpLocation.Header)
			.DistinctBy(d => d.Location.Name ?? d.Name)
			.ToLookup(g => g.IsNullable && String.IsNullOrEmpty(g.Location.Format));

		var bodies = items
			.Where(w => w.Location.Location == HttpLocation.Body);

		if (!headers.Any() && !bodies.Any())
		{
			WriteMethodBodyWithoutHeaders(method, clientName, items, tokenText, builder);
		}
		else
		{
			WriteMethodBodyWithHeaders(method, clientName, items, tokenText, headers, bodies.FirstOrDefault(), builder);
		}

		WriteMethodReturn(method, tokenText, builder);

		var optionalQueries = items
			.Where(w => w.Location.Location == HttpLocation.Query && w.IsNullable || w.Location.Location == HttpLocation.QueryMap)
			.ToList();

		if (optionalQueries.Any())
		{
			WriteCreatePathMethod(method, optionalQueries, builder);
		}
	}

	private static void WriteMethodBodyWithoutHeaders(MethodModel method, string clientName, IEnumerable<IType> items, string tokenText, SourceWriter builder)
	{
		if (method.ReturnType is nameof(HttpResponseMessage))
		{
			builder.WriteLine($"var response = await {clientName}.{method.Method?.Method ?? "Get"}Async({ParsePath(method.Path, items)}, {tokenText});");
		}
		else
		{
			builder.WriteLine($"using var response = await {clientName}.{method.Method?.Method ?? "Get"}Async({ParsePath(method.Path, items)}, {tokenText});");
		}
	}

	private static void WriteMethodBodyWithHeaders(MethodModel method, string clientName, IEnumerable<IType> items, string tokenText, ILookup<bool, IType> headers, IType? body, SourceWriter builder)
	{
		builder.WriteLine($"using var request = new HttpRequestMessage(HttpMethod.{method.Method?.Method ?? "Get"}, {ParsePath(method.Path, items)});");

		if (headers[false].Any())
		{
			builder.WriteLine();
		}

		foreach (var header in headers[false])
		{
			WriteHeader(header, builder);
		}

		foreach (var header in headers[true])
		{
			WriteNullableHeader(header, builder);
		}

		if (body != null)
		{
			builder.WriteLine();
			WriteRequestBody(body, builder);
		}

		builder.WriteLine();

		if (method.ReturnType is nameof(HttpResponseMessage))
		{
			builder.WriteLine($"var response = await {clientName}.SendAsync(request, {tokenText});");
		}
		else
		{
			builder.WriteLine($"using var response = await {clientName}.SendAsync(request, {tokenText});");
		}
	}

	private static void WriteHeader(IType header, SourceWriter builder)
	{
		var format = !String.IsNullOrEmpty(header.Location.Format)
			? $"\"{header.Location.Format}\""
			: String.Empty;

		var result = $"{header.Name}.ToString({format})";

		if (HasHoles(header.Location.Format))
		{
			result = $"$\"{FillHoles(header.Location.Format, header.Name)}\"";
		}

		builder.WriteLine($"request.Headers.Add(\"{header.Location.Name}\", {result});");
	}

	private static void WriteNullableHeader(IType header, SourceWriter builder)
	{
		var format = !String.IsNullOrEmpty(header.Location.Format)
			? $"\"{header.Location.Format}\""
			: String.Empty;

		var suffix = header is { Namespace: "System", Type: "String" or "string" }
			? String.Empty
			: $".ToString({format})";

		var result = $"{header.Name}{suffix}";

		if (HasHoles(header.Location.Format))
		{
			result = $"$\"{FillHoles(header.Location.Format, header.Name)}\"";
		}

		builder.WriteLine();
		builder.WriteLine($"if ({header.Name} != null)");
		builder.WriteLine('{');
		builder.WriteLine($"\trequest.Headers.Add(\"{header.Location.Name}\", {result});");
		builder.WriteLine('}');
	}

	private static void WriteMethodReturn(MethodModel method, string tokenText, SourceWriter builder)
	{
		if (!method.AllowAnyStatusCode)
		{
			builder.WriteLine();
			builder.WriteLine("response.EnsureSuccessStatusCode();");
		}

		switch (method.ReturnType)
		{
			case nameof(String) or "string":
				builder.WriteLine();
				builder.WriteLine($"return await response.Content.ReadAsStringAsync({tokenText});");
				break;
			case nameof(HttpResponseMessage):
				builder.WriteLine();
				builder.WriteLine("return response;");
				break;
			case nameof(Stream):
				builder.WriteLine();
				builder.WriteLine($"return await response.Content.ReadAsStreamAsync({tokenText});");
				break;
			case "byte[]":
				builder.WriteLine();
				builder.WriteLine($"return await response.Content.ReadAsByteArrayAsync({tokenText});");
				break;

			default:
			{
				if (!String.IsNullOrEmpty(method.ReturnType))
				{
					builder.WriteLine();
					builder.WriteLine($"return await response.Content.ReadFromJsonAsync<{method.ReturnType}>({tokenText});");
				}

				break;
			}
		}
	}

	private static void WriteCreatePathMethod(MethodModel method, List<IType> optionalQueries, SourceWriter builder)
	{
		var hasQueries = method.Path.Contains("?");

		builder.WriteLine();
		builder.WriteLine("string CreatePath()");
		builder.WriteLine('{');
		builder.Indentation++;

		builder.WriteLine($"DefaultInterpolatedStringHandler handler = $\"{GetPath(method.Path, method.Parameters)}\";");

		if (!hasQueries)
		{
			builder.WriteLine("var hasQueries = false;");
		}

		// builder.WriteLine();

		foreach (var query in optionalQueries)
		{
			WriteOptionalQuery(query, hasQueries, optionalQueries.Count, builder);
		}

		builder.WriteLine();
		builder.WriteLine("return handler.ToStringAndClear();");

		builder.Indentation--;
		builder.WriteLine('}');
	}

	private static void WriteOptionalQuery(IType query, bool hasQueries, int queryCount, SourceWriter builder)
	{
		if (query.Location is { Location: HttpLocation.QueryMap } && query is ParameterModel { GenericTypes: { Length: 2 } genericTypes })
		{
			builder.WriteLine();
			builder.WriteLine($"foreach (var item in {query.Name})");
			builder.WriteLine('{');
			
			builder.Indentation++;

			if (!genericTypes[1].IsCollection && genericTypes[1].IsNullable)
			{
				builder.WriteLine($"if (item.Value == null)");
				builder.WriteLine('{');
				builder.WriteLine("\tcontinue;");
				builder.WriteLine('}');
				builder.WriteLine();
			}

			builder.WriteLine($"var key = {ParseField("item.Key", genericTypes[0], query.Location)};");
			builder.WriteLine();
			
			if (genericTypes[1].IsCollection)
			{
				builder.WriteLine("foreach (var value in item.Value)");
				builder.WriteLine('{');

				builder.Indentation++;

				if (genericTypes[1].CollectionType.IsNullable)
				{
					builder.WriteLine($"if (value == null)");
					builder.WriteLine('{');
					builder.WriteLine("\tcontinue;");
					builder.WriteLine('}');
					builder.WriteLine();
				}
				
				if (hasQueries)
				{
					builder.WriteLine("handler.AppendLiteral(\"&\");");
				}
				else
				{
					builder.WriteLine("handler.AppendLiteral(hasQueries ? \"&\" : \"?\");");
				}
				
				builder.WriteLine("handler.AppendFormatted(key);");
				builder.WriteLine("handler.AppendLiteral(\"=\");");
				builder.WriteLine($"handler.AppendFormatted({ParseField("value", genericTypes[1], query.Location)});");
				
				if (!hasQueries)
				{
					builder.WriteLine();
					builder.WriteLine("hasQueries = true;");
				}
				
				builder.Indentation--;
				
				builder.WriteLine('}');
			}
			else
			{
				if (hasQueries)
				{
					builder.WriteLine("handler.AppendLiteral(\"&\");");
				}
				else
				{
					builder.WriteLine("handler.AppendLiteral(hasQueries ? \"&\" : \"?\");");
				}
				
				builder.WriteLine("handler.AppendFormatted(key);");
				builder.WriteLine("handler.AppendLiteral(\"=\");");
				builder.WriteLine($"handler.AppendFormatted({ParseField("item.Value", genericTypes[1], query.Location)});");

				if (!hasQueries)
				{
					builder.WriteLine();
					builder.WriteLine("hasQueries = true;");
				}
			}
			
			

			builder.Indentation--;

			builder.WriteLine('}');
			
			return;
		}
		
		builder.WriteLine($"if ({query.Name} != null)");
		builder.WriteLine('{');

		if (hasQueries)
		{
			builder.WriteLine($"\thandler.AppendLiteral(\"&{query.Location.Name}=\");");
		}
		else
		{
			if (queryCount > 1)
			{
				builder.WriteLine("\thandler.AppendLiteral(hasQueries ? \"&\" : \"?\");");
				builder.WriteLine($"\thandler.AppendLiteral(\"{query.Location.Name}=\");");
			}
			else
			{
				builder.WriteLine($"\thandler.AppendLiteral(\"?{query.Location.Name}=\");");
			}
		}

		if (query.Location.UrlEncode)
		{
			if (query is { Namespace: "System", Type: "String" or "string" })
			{
				builder.WriteLine($"\thandler.AppendFormatted(Uri.EscapeDataString({query.Name}));");
			}
			else
			{
				builder.WriteLine($"\thandler.AppendFormatted(Uri.EscapeDataString({query.Name}.ToString()));");
			}
		}
		else
		{
			builder.WriteLine($"\thandler.AppendFormatted({query.Name});");
		}

		if (queryCount > 1)
		{
			builder.WriteLine();
			builder.WriteLine("\thasQueries = true;");
		}


		builder.WriteLine('}');
	}

	private static void WriteClassEnd(SourceWriter builder)
	{
		builder.Indentation = Math.Max(builder.Indentation - 1, 0);

		builder.WriteLine('}');
	}

	private static string ParsePath(string path, IEnumerable<IType> parameters)
	{
		var hasHoles = parameters.Any(a => a.Location.Location is HttpLocation.Path or HttpLocation.Query or HttpLocation.QueryMap);

		path = GetPath(path, parameters);

		if (hasHoles)
		{
			if (parameters.Any(a => a.Location.Location == HttpLocation.Query && a.IsNullable || a.Location.Location == HttpLocation.QueryMap))
			{
				return $"CreatePath()";
			}

			return $"$\"{path}\"";
		}

		return $"\"{path}\"";
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

		var queryIndex = uriToBeAppended.IndexOf('?');
		var hasQuery = queryIndex != -1;

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

	private static bool HasHoles(string value)
	{
		return HoleRegex.IsMatch(value);
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

	private static string GetPath(string path, IEnumerable<IType> parameters)
	{
		var hasHoles = parameters.Any(a => a.Location.Location is HttpLocation.Path or HttpLocation.Query);

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
					.Replace("{", "{{")
					.Replace("}", "}}"));

				if (pathHoles.TryGetValue(match.Value.Substring(1, match.Value.Length - 2), out var parameter))
				{
					resultPath.Append($"{{{parameter.Name}}}");
				}
				else
				{
					resultPath.Append(match.Value
						.Replace("{", "{{")
						.Replace("}", "}}"));
				}

				index = match.Index + match.Length;
			}

			resultPath.Append(path
				.Substring(index)
				.Replace("{", "{{")
				.Replace("}", "}}"));

			path = resultPath.ToString();
		}

		path = AddQueryString(path, parameters
			.Where(w => w.Location.Location == HttpLocation.Query && !w.IsNullable)
			.Select(s => new KeyValuePair<string, string>(s.Location.Name ?? s.Name, String.IsNullOrEmpty(s.Location.Format)
				? $"{{{s.Name}}}"
				: $"{{{s.Name}:{s.Location.Format}}}")));

		return path;
	}

	private static void WriteRequestBody(IType body, SourceWriter builder)
	{
		switch (body)
		{
			case { Namespace: "System", Type: "string" or "String" }:
				builder.WriteLine($"request.Content = new StringContent({body.Name});");
				break;
			case { Namespace: "System", Type: "byte[]" }: 
				builder.WriteLine($"request.Content = new ByteArrayContent({body.Name});");
				break;
			case { Namespace: "System.IO", Type: "Stream" }:
				builder.WriteLine($"request.Content = new StreamContent({body.Name});");
				break;
			default:
				builder.WriteLine($"request.Content = JsonContent.Create({body.Name});");
				break;
		}
	}

	private static string ParseField(string fieldName, TypeModel type, LocationAttributeModel location)
	{
		if (type is { Namespace: "System", Type: "String" or "string" } && location.UrlEncode)
		{
			return $"Uri.EscapeDataString({fieldName})";
		}
		
		if (location.UrlEncode && NeedsUrlEncode(type.Namespace, type.Type))
		{
			var format = !String.IsNullOrEmpty(location.Format)
				? $"\"{location.Format}\""
				: String.Empty;
			
			return $"Uri.EscapeDataString({fieldName}.ToString({format}))";
		}

		if (!String.IsNullOrEmpty(location.Format))
		{ 
			return $"{fieldName}.ToString(\"{location.Format}\")";
		}

		return $"{fieldName}";
	}
	
	private static bool NeedsUrlEncode(string @namespace, string type)
	{
		return (@namespace is "System" &&
		         type is "Int16" or "Int32" or "Int64" or "UInt16" or "UInt32" or "UInt64" or "Single" or "Double" or "Decimal" or "Boolean" or "Char");
	}
}