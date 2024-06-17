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
			$"{nameof(Literals.BaseAddressAttribute)}.g.cs",
			SourceText.From(Literals.AttributeSourceCode, Encoding.UTF8)));

		context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
			$"{nameof(Literals.RequestAttributes)}.g.cs",
			SourceText.From(Literals.RequestAttributes, Encoding.UTF8)));

		context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
			$"{nameof(Literals.QueryAttributes)}.g.cs",
			SourceText.From(Literals.QueryAttributes, Encoding.UTF8)));

		context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
			$"{nameof(Literals.QuerySerializationMethod)}.g.cs",
			SourceText.From(Literals.QuerySerializationMethod, Encoding.UTF8)));

		context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
			$"{nameof(Literals.AllowAnyStatusCodeAttribute)}.g.cs",
			SourceText.From(Literals.AllowAnyStatusCodeAttribute, Encoding.UTF8)));

		context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
			$"{nameof(Literals.HeaderAttribute)}.g.cs",
			SourceText.From(Literals.HeaderAttribute, Encoding.UTF8)));

		context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
			$"{nameof(Literals.PathAttribute)}.g.cs",
			SourceText.From(Literals.PathAttribute, Encoding.UTF8)));

		context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
			$"{nameof(Literals.BodyAttribute)}.g.cs",
			SourceText.From(Literals.BodyAttribute, Encoding.UTF8)));

		context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
			$"{nameof(Literals.RestClientAttribute)}.g.cs",
			SourceText.From(Literals.RestClientAttribute, Encoding.UTF8)));

		context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
			$"{nameof(Literals.QueryMapAttribute)}.g.cs",
			SourceText.From(Literals.QueryMapAttribute, Encoding.UTF8)));
		
		context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
			$"{nameof(Literals.RawQueryStringAttribute)}.g.cs",
			SourceText.From(Literals.RawQueryStringAttribute, Encoding.UTF8)));

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
				"System.Net.Http.Headers",
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

		using (builder.AppendIndentation("DefaultRequestHeaders = "))
		{
			foreach (var header in source.Attributes.Where(w => w.Location == HttpLocation.Header))
			{
				var result = ParseHeader(header, (header, value) => $"{{ \"{header}\", \"{value}\" }}");
				
				builder.WriteLine($"{result},");
			}
		}

		builder.Indentation = 0;
	}

	private static void WriteMethods(ClassModel source, SourceWriter builder)
	{
		foreach (var method in source.Methods)
		{
			WriteMethod(method, source, builder);
		}

		if (source.IsDisposable)
		{
			builder.WriteLine();

			using (builder.AppendIndentation("public void Dispose()"))
			{
				builder.WriteLine($"{source.ClientName}.Dispose();");
			}
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

		using (builder.AppendIndentation($"public partial async {returnType} {method.Name}({String.Join(", ", method.Parameters.Select(s => $"{s.Type} {s.Name}"))})"))
		{
			var items = method.Parameters
				.Concat<IType>(source.Properties);

			WriteMethodBody(method, source.ClientName, items, tokenText, builder);
		}
	}

	private static void WriteMethodBody(MethodModel method, string clientName, IEnumerable<IType> items, string tokenText, SourceWriter builder)
	{
		var headers = items
			.Where(w => w.Location.Location == HttpLocation.Header)
			.DistinctBy(d => d.Location.Name ?? d.Name)
			.ToLookup(g => g.IsNullable && g.NullableAnnotation == NullableAnnotation.Annotated && String.IsNullOrEmpty(g.Location.Format));

		var methodDefaultItems = method.Locations
			.Where(w => w.Location == HttpLocation.Header);

		var requiredParameters = items
			.Where(w => w.IsNullable && w.NullableAnnotation == NullableAnnotation.NotAnnotated)
			.ToList();

		var bodies = items
			.Where(w => w.Location.Location == HttpLocation.Body);

		if (requiredParameters.Any())
		{
			foreach (var parameter in requiredParameters)
			{
				builder.WriteLine($"ArgumentNullException.ThrowIfNull({parameter.Name});");
			}

			builder.WriteLine();
		}

		if (!headers.Any() && !bodies.Any() && !methodDefaultItems.Any())
		{
			WriteMethodBodyWithoutHeaders(method, clientName, items, tokenText, builder);
		}
		else
		{
			WriteMethodBodyWithHeaders(method, clientName, items, tokenText, headers, methodDefaultItems.ToDictionary(t => t.Name, t => t), bodies.FirstOrDefault(), builder);
		}

		WriteMethodReturn(method, tokenText, builder);

		var optionalQueries = items
			.Where(w => w.Location.Location == HttpLocation.Query && w is { IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated } || w.Location.Location == HttpLocation.QueryMap || (w.Location.Location == HttpLocation.Raw && w.IsNullable))
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

	private static void WriteMethodBodyWithHeaders(MethodModel method, string clientName, IEnumerable<IType> items, string tokenText, ILookup<bool, IType> headers, Dictionary<string, LocationAttributeModel> defaultItems, IType? body, SourceWriter builder)
	{
		builder.WriteLine($"using var request = new HttpRequestMessage(HttpMethod.{method.Method?.Method ?? "Get"}, {ParsePath(method.Path, items)});");

		var defaultHeaders = defaultItems
			.Where(item => headers[false].All(a => a.Name != item.Key) && headers[true].All(a => a.Name != item.Key))
			.ToList();
		
		if (headers[false].Any() || defaultHeaders.Any())
		{
			builder.WriteLine();
		}

		foreach (var header in headers[false])
		{
			WriteHeader(header, defaultItems.TryGetValue(header.Name, out var value) ? value : null, builder);
		}

		foreach (var header in headers[true])
		{
			WriteNullableHeader(header, defaultItems.TryGetValue(header.Name, out var value) ? value : null, builder);
		}

		foreach (var item in defaultHeaders)
		{
			var result = ParseHeader(item.Value, (header, value) => $"Add(\"{item.Key}\", \"{item.Value.Value}\");");	
			builder.WriteLine($"request.Headers.{result};");
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

	private static void WriteHeader(IType header, LocationAttributeModel? defaultItem, SourceWriter builder)
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

		if (defaultItem is null)
		{
			builder.WriteLine($"request.Headers.Add(\"{header.Location.Name ?? header.Name}\", {result});");
		}
		else
		{
			builder.WriteLine($"request.Headers.Add(\"{header.Location.Name ?? header.Name}\", {result} ?? \"{defaultItem.Value}\");");
		}
	}

	private static void WriteNullableHeader(IType header, LocationAttributeModel? defaultItems, SourceWriter builder)
	{
		var format = !String.IsNullOrEmpty(header.Location.Format)
			? $"\"{header.Location.Format}\""
			: String.Empty;
		
		var suffix = header is { Namespace: "System", Type: "String" or "string" }
			? String.Empty
			: $".ToString({format})";

		if (header.IsNullable && defaultItems != null)
		{
			suffix = '?' + suffix;
		}
		
		var result = $"{header.Name}{suffix}";
		
		if (HasHoles(header.Location.Format))
		{
			result = $"$\"{FillHoles(header.Location.Format, header.Name)}\"";
		}

		if (defaultItems is null)
		{
			builder.WriteLine();

			using (builder.AppendIndentation($"if ({header.Name} is not null)"))
			{
				builder.WriteLine($"request.Headers.Add(\"{header.Location.Name}\", {result});");
			}
		}
		else
		{
			builder.WriteLine($"request.Headers.Add(\"{header.Location.Name}\", {result} ?? \"{defaultItems.Value}\");");
		}
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

		using (builder.AppendIndentation("string CreatePath()"))
		{
			var hasVariable = !hasQueries && ((optionalQueries.Count > 1 || optionalQueries[0].Location.Location == HttpLocation.QueryMap) || optionalQueries.Any(a => a is ParameterModel { IsCollection: true }));

			builder.WriteLine($"DefaultInterpolatedStringHandler handler = $\"{GetPath(method.Path, method.Parameters)}\";");

			if (hasVariable)
			{
				builder.WriteLine("var hasQueries = false;");
			}

			for (int i = 0; i < optionalQueries.Count; i++)
			{
				WriteOptionalQuery(optionalQueries[i] as ParameterModel, hasQueries, hasVariable, i == 0, optionalQueries.Count, builder);
			}

			builder.WriteLine();
			builder.WriteLine("return handler.ToStringAndClear();");
		}
	}

	private static void WriteOptionalQuery(ParameterModel query, bool hasQueries, bool hasVariable, bool isFirst, int queryCount, SourceWriter builder)
	{
		if (query.Location is { Location: HttpLocation.QueryMap } && query is { GenericTypes: { Length: 2 } genericTypes })
		{
			builder.WriteLine();

			if (query is { IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated })
			{
				builder.WriteLine($"if ({query.Name} != null)");
				builder.WriteLine('{');
				builder.Indentation++;
			}

			builder.WriteLine($"foreach (var item in {query.Name})");
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
				
				builder.WriteLine("foreach (var value in item.Value)");
				builder.WriteLine('{');

				builder.Indentation++;

				if (genericTypes[1].CollectionType is { IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated })
				{
					AppendContinue("value");
				}

				if (query.Location.UrlEncode && NeedsUrlEncode(genericTypes[0].Namespace, genericTypes[0].Type))
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
				AppendQueryItem("item.Value", String.Empty, genericTypes[1]);
			}

			if (query is { IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated })
			{
				builder.Indentation--;
				builder.WriteLine('}');
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
		}

		builder.Indentation++;

		if (query.IsCollection)
		{
			builder.WriteLine($"foreach (var item in {query.Name})");
			builder.WriteLine('{');
			builder.Indentation++;

			if (query.CollectionItemType.IsNullable && query.CollectionItemType.NullableAnnotation == NullableAnnotation.Annotated)
			{
				AppendContinue("item");
			}

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
			
			builder.Indentation--;

			if (query is { IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated })
			{
				builder.WriteLine('}');
			}
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

		builder.Indentation--;
		builder.WriteLine('}');
		
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

	private static void WriteClassEnd(SourceWriter builder)
	{
		builder.Indentation = Math.Max(builder.Indentation - 1, 0);

		builder.WriteLine('}');
	}

	private static string ParsePath(string path, IEnumerable<IType> parameters)
	{
		var hasHoles = parameters.Any(a => a.Location.Location is HttpLocation.Path or HttpLocation.Query or HttpLocation.QueryMap or HttpLocation.Raw);

		path = GetPath(path, parameters);

		if (hasHoles)
		{
			if (parameters.Any(a => a.Location.Location == HttpLocation.Query && (a.IsNullable && a.NullableAnnotation == NullableAnnotation.Annotated) || a.Location.Location == HttpLocation.QueryMap || (a.Location.Location == HttpLocation.Raw && a.IsNullable)))
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

			resultPath.Append(path
				.Substring(index)
				.Replace("{", "%7B")
				.Replace("}", "%7D"));

			path = resultPath.ToString();
		}

		path = AddQueryString(path, parameters
			.Where(w => w.Location.Location == HttpLocation.Query && (!w.IsNullable || w.NullableAnnotation == NullableAnnotation.NotAnnotated))
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

	private static string ParseHeader(LocationAttributeModel location, Func<string, string, string> defaultBuilder)
	{
		switch (location.Name)
		{
			case "Accept":
				return $"Accept.Add(new MediaTypeWithQualityHeaderValue(\"{location.Value}\"))";
			case "Accept-Charset":
				return $"AcceptCharset.Add(new StringWithQualityHeaderValue(\"{location.Value}\"))";
			case "Accept-Encoding":
				return $"AcceptEncoding.Add(new StringWithQualityHeaderValue(\"{location.Value}\"))";
			case "Accept-Language":
				return $"AcceptLanguage.Add(new StringWithQualityHeaderValue(\"{location.Value}\"))";
			case "Authorization":
				var authorizationArguments = location.Value.Split([' '], StringSplitOptions.RemoveEmptyEntries)
					.Select(s => $"\"{s}\"")
					.Take(2);
				
				return $"Authorization = new AuthenticationHeaderValue({String.Join(", ", authorizationArguments)})";
			case "Connection":
				return $"Connection.Add(\"{location.Value}\")";
			case "ConnectionClose" when Boolean.TryParse(location.Value, out var result):
				return $"ConnectionClose = {result.ToString().ToLower()}";
			case "Date" when DateTime.TryParse(location.Value, out _):
				return $"Date = DateTimeOffset.Parse(\"{location.Value}\")";
			case "Expect":
				return $"Expect.Add(new NameValueWithParametersHeaderValue(\"{location.Value}\"))";
			case "ExpectContinue" when Boolean.TryParse(location.Value, out var result):
				return $"ExpectContinue = {result.ToString().ToLower()}";
			case "From":
				return $"From = \"{location.Value}\"";
			case "Host":
				return $"Host = \"{location.Value}\"";
			case "If-Match":
				return $"IfMatch.Add(new EntityTagHeaderValue(\"{location.Value}\"))";
			case "If-Modified-Since" when DateTime.TryParse(location.Value, out _):
				return $"IfModifiedSince = DateTimeOffset.Parse(\"{location.Value}\")";
			case "If-None-Match":
				return $"IfNoneMatch.Add(new EntityTagHeaderValue(\"{location.Value}\"))";
			case "If-Range":
				return $"IfRange = new RangeConditionHeaderValue(\"{location.Value}\")";
			case "If-Unmodified-Since" when DateTime.TryParse(location.Value, out _):
				return $"IfUnmodifiedSince = DateTimeOffset.Parse(\"{location.Value}\")";
			case "Max-Forwards" when Int32.TryParse(location.Value, out var result):
				return $"MaxForwards = {result}";
			case "Pragma":
				return $"Pragma.Add(new NameValueHeaderValue(\"{location.Value}\"))";
			case ":protocol":
				return $"Protocol = \"{location.Value}\"";
			case "Proxy-Authorization":
				var proxyArguments = location.Value.Split([' '], StringSplitOptions.RemoveEmptyEntries)
					.Select(s => $"\"{s}\"")
					.Take(2);
				
				return $"ProxyAuthorization = new AuthenticationHeaderValue({String.Join(", ", proxyArguments)})";
			case "Referer":
				return $"Referrer = new Uri(\"{location.Value}\", UriKind.RelativeOrAbsolute)";
			case "TE":
				return $"TE.Add(new TransferCodingWithQualityHeaderValue(\"{location.Value}\"))";
			case "Trailer":
				return $"Trailer.Add(\"{location.Value}\")";
			case "Transfer-Encoding":
				return $"TransferEncoding.Add(new TransferCodingHeaderValue(\"{location.Value}\"))";
			case "TransferEncodingChunked" when Boolean.TryParse(location.Value, out var result):
				return $"TransferEncodingChunked = {result.ToString().ToLower()}";
			case "Upgrade":
				var upgradeArguments = location.Value.Split([' '], StringSplitOptions.RemoveEmptyEntries)
					.Select(s => $"\"{s}\"")
					.Take(2);
				return $"Upgrade.Add(new ProductHeaderValue({String.Join(", ", upgradeArguments)}))";
			case "User-Agent":
				var userAgentArguments = location.Value.Split([' '], StringSplitOptions.RemoveEmptyEntries)
					.Select(s => $"\"{s}\"")
					.Take(2);
				
				return $"UserAgent.Add(new ProductInfoHeaderValue({String.Join(", ", userAgentArguments)}))";
			case "Via":
				var viaArguments = location.Value.Split([' '], StringSplitOptions.RemoveEmptyEntries)
					.Select(s => $"\"{s}\"")
					.Take(4);
				
				return $"Via.Add(new ViaHeaderValue({String.Join(", ", viaArguments)}))";
			default:
				return defaultBuilder(location.Name, location.Value);
		}
	}
}