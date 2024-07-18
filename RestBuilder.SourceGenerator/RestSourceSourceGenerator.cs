using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using RestBuilder.Core.Attributes;
using RestBuilder.SourceGenerator.Enumerators;
using RestBuilder.SourceGenerator.Helpers;
using RestBuilder.SourceGenerator.Interfaces;
using RestBuilder.SourceGenerator.Models;
using RestBuilder.SourceGenerator.Parsers;
using RestBuilder.SourceGenerator.Writers;
using TypeShape.Roslyn;

namespace RestBuilder.SourceGenerator;

[Generator]
public class RestSourceSourceGenerator : IIncrementalGenerator
{
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		var classes = context.SyntaxProvider
			.ForAttributeWithMetadataName(
				$"RestBuilder.Core.Attributes.{nameof(RestClientAttribute)}",
				(node, token) => !token.IsCancellationRequested && node is ClassDeclarationSyntax classDeclaration && classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)),
				GenerateSource);

		context.RegisterSourceOutput(classes.Combine(context.CompilationProvider), static (spc, source) => Execute(source.Left, source.Right, spc));

		void RegisterSource(string sourceCode, [CallerArgumentExpression(nameof(sourceCode))] string attributeName = null!)
		{
			context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
				$"{attributeName.Split('.')[^1]}.g.cs",
				SourceText.From(sourceCode, Encoding.UTF8)));
		}
	}

	private static ClassModel? GenerateSource(GeneratorAttributeSyntaxContext context, CancellationToken token)
	{
		if (!token.IsCancellationRequested && context is { TargetNode: ClassDeclarationSyntax classDeclarationSyntax, TargetSymbol: INamedTypeSymbol namedTypeSymbol })
		{
			return ClassParser.Parse(classDeclarationSyntax, namedTypeSymbol, context.Attributes, context.SemanticModel.Compilation);
		}

		return null;
	}

	private static void Execute(ClassModel? source, Compilation compilation, SourceProductionContext context)
	{
		if (compilation is CSharpCompilation csharpCompilation)
		{
			Debug.WriteLine(csharpCompilation.LanguageVersion.MapSpecifiedToEffectiveVersion());
		}

		if (source is null)
		{
			return;
		}

		var builder = new SourceWriter('\t', 1);

		WriteNamespaces(source, builder);
		WriteClassStart(source, compilation, builder);
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
				"System.Linq",
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

	private static void WriteClassStart(ClassModel source, Compilation compilation, SourceWriter builder)
	{
		var hasHeaders = source.Attributes.Any(a => a.Location == HttpLocation.Header);

		if (compilation.Options.NullableContextOptions != NullableContextOptions.Enable)
		{
			builder.WriteLine($$"""
					
				namespace {{source.Namespace}};

				#nullable enable

				public partial class {{source.Name}}
				{
				""");
		}
		else
		{
			builder.WriteLine($$"""
					
				namespace {{source.Namespace}};

				public partial class {{source.Name}}
				{
				""");
		}

		if (source.NeedsClient)
		{
			builder.Indentation = 1;

			if (source.HttpClientInitializer is not null)
			{
				builder.WriteLine($"public HttpClient {source.ClientName} {{ get; }} = {source.HttpClientInitializer}();");
			}
			else
			{
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
					HeaderWriter.WriteDefaultRequestHeaders(source, builder);
				}

				if (hasHeaders || !String.IsNullOrEmpty(source.BaseAddress))
				{
					builder.WriteLine("};");
				}
			}

			builder.Indentation = 0;
		}
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
			builder.WriteLine("/// <summary>");
			builder.WriteLine("/// Disposes the HttpClient.");
			builder.WriteLine("/// </summary>");
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

		if (!String.IsNullOrEmpty(method.ReturnTypeName))
		{
			returnType = method.ReturnTypeName;
		}

		builder.WriteLine();

		builder.Indentation++;

		var asyncKeyword = method.IsAwaitable
			? "async "
			: String.Empty;

		CommentWriter.WriteComment(builder, source, method);
		
		using (builder.AppendIndentation($"public partial {asyncKeyword}{returnType} {method.Name}({String.Join(", ", method.Parameters.Select(s => $"{s.Type} {s.Name}"))})"))
		{
			if (method.IsAwaitable)
			{
				var items = method.Parameters
					.Concat<IType>(source.Properties)
					.ToList();

				WriteMethodBody(method, source.ClientName, items, source, tokenText, builder);
			}
		}
	}

	private static void WriteMethodBody(MethodModel method, string clientName, List<IType> items, ClassModel classModel, string tokenText, SourceWriter builder)
	{
		var headers = items
			.Where(w => w.Location.Location == HttpLocation.Header)
			.DistinctBy(d => d.Location.Name ?? d.Name)
			.ToLookup(g => g.IsNullable && g.NullableAnnotation == NullableAnnotation.Annotated && String.IsNullOrEmpty(g.Location.Format));

		var methodDefaultItems = method.Locations
			.Where(w => w.Location == HttpLocation.Header)
			.ToList();

		var requiredParameters = items
			.Where(w => w.IsNullable && w.NullableAnnotation == NullableAnnotation.NotAnnotated)
			.ToList();

		var bodies = items
			.Where(w => w.Location.Location == HttpLocation.Body)
			.ToList();

		if (requiredParameters.Any())
		{
			foreach (var parameter in requiredParameters)
			{
				builder.WriteLine($"ArgumentNullException.ThrowIfNull({parameter.Name});");
			}

			builder.WriteLine();
		}

		if (!headers.Any() && !bodies.Any() && !methodDefaultItems.Any() && classModel.RequestModifiers.Length == 0)
		{
			WriteMethodBodyWithoutHeaders(method, clientName, items, tokenText, classModel.RequestQueryParamSerializers, builder);
		}
		else
		{
			WriteMethodBodyWithHeaders(method, clientName, items, tokenText, headers, methodDefaultItems.ToDictionary(t => t.Name ?? String.Empty, t => t), bodies.FirstOrDefault(), classModel, classModel.RequestQueryParamSerializers, builder);
		}

		WriteMethodReturn(method, classModel.ResponseDeserializers, tokenText, builder);

		var optionalQueries = items
			.Where(w => w.Location.Location == HttpLocation.Query 
				&& (w is { IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated } 
				    || PathWriter.GetQueryParamSerializer(classModel.RequestQueryParamSerializers, w) != null) 
				|| w.Location.Location == HttpLocation.QueryMap 
				|| (w.Location.Location == HttpLocation.Raw && w.IsNullable))
			.ToList();

		if (optionalQueries.Any())
		{
			PathWriter.WriteCreatePathMethod(method, classModel.RequestQueryParamSerializers, optionalQueries, builder);
		}
	}

	private static void WriteMethodBodyWithoutHeaders(MethodModel method, string clientName, IEnumerable<IType> items, string tokenText, ImmutableEquatableArray<RequestQueryParamSerializerModel> queryParamSerializers, SourceWriter builder)
	{
		Debug.WriteLine(method.ReturnType);
		
		if (method.ReturnTypeName is nameof(HttpResponseMessage))
		{
			builder.WriteLine($"var response = await {clientName}.{method.Method.Method ?? "Get"}Async({PathWriter.ParsePath(method.Path, items, queryParamSerializers)}, {tokenText});");
		}
		else if (method.ReturnType is { Type: "void", Namespace: "System" })
		{
			builder.WriteLine($"await {clientName}.{method.Method.Method ?? "Get"}Async({PathWriter.ParsePath(method.Path, items, queryParamSerializers)}, {tokenText});");
		}
		else
		{
			builder.WriteLine($"using var response = await {clientName}.{method.Method.Method ?? "Get"}Async({PathWriter.ParsePath(method.Path, items, queryParamSerializers)}, {tokenText});");
		}
	}

	private static void WriteMethodBodyWithHeaders(MethodModel method, string clientName, IEnumerable<IType> items, string tokenText, ILookup<bool, IType> headers, Dictionary<string, LocationAttributeModel> defaultItems, IType? body, ClassModel classModel, ImmutableEquatableArray<RequestQueryParamSerializerModel> queryParamSerializers, SourceWriter builder)
	{
		builder.WriteLine($"using var request = new HttpRequestMessage(HttpMethod.{method.Method.Method ?? "Get"}, {PathWriter.ParsePath(method.Path, items, queryParamSerializers)});");

		var defaultHeaders = defaultItems
			.Where(item => headers[false].All(a => a.Name != item.Key) && headers[true].All(a => a.Name != item.Key))
			.ToList();

		if (headers[false].Any() || defaultHeaders.Any())
		{
			builder.WriteLine();
		}

		foreach (var header in headers[false])
		{
			HeaderWriter.WriteHeader(header, defaultItems.TryGetValue(header.Name, out var value) ? value : null, builder);
		}

		foreach (var header in headers[true])
		{
			HeaderWriter.WriteNullableHeader(header, defaultItems.TryGetValue(header.Name, out var value) ? value : null, builder);
		}

		foreach (var item in defaultHeaders)
		{
			var result = HeaderWriter.ParseHeader(item.Value, (_, _) => $"Add(\"{item.Key}\", \"{item.Value.Value}\");");
			builder.WriteLine($"request.Headers.{result};");
		}

		if (body != null)
		{
			builder.WriteLine();
			BodyWriter.WriteRequestBody(body, classModel, tokenText, builder);
		}

		builder.WriteLine();

		if (classModel.RequestModifiers.Length > 0)
		{
			foreach (var requestModifier in classModel.RequestModifiers)
			{
				var awaitPrefix = requestModifier.IsAsync
					? "await "
					: String.Empty;

				var cancellationTokenSuffix = requestModifier.HasCancellation
					? $", {tokenText}"
					: String.Empty;

				builder.WriteLine($"{awaitPrefix}{requestModifier.Name}(request{cancellationTokenSuffix});");
			}

			builder.WriteLine();
		}
		
		Debug.WriteLine(method.ReturnType);

		if (method.ReturnTypeName is nameof(HttpResponseMessage))
		{
			builder.WriteLine($"var response = await {clientName}.SendAsync(request, {tokenText});");
		}
		else if (method.ReturnType is { Type: "void", Namespace: "System" })
		{
			builder.WriteLine($"await {clientName}.SendAsync(request, {tokenText});");
		}
		else
		{
			builder.WriteLine($"using var response = await {clientName}.SendAsync(request, {tokenText});");
		}
	}


	private static void WriteMethodReturn(MethodModel method, ImmutableEquatableArray<ResponseDeserializerModel> responseDeserializers, string tokenText, SourceWriter builder)
	{
		if (!method.AllowAnyStatusCode)
		{
			builder.WriteLine();
			builder.WriteLine("response.EnsureSuccessStatusCode();");
		}

		if (method.ReturnType is { Namespace: "System", Name: "Void" })
		{
			return;
		}

		foreach (var deserializer in responseDeserializers)
		{
			if (deserializer is { Type.IsGeneric: false } && ClassParser.TypeEquals(method.ReturnType, deserializer.Type))
			{
				var awaitPrefix = deserializer.IsAsync
					? "await "
					: String.Empty;

				var cancellationTokenSuffix = deserializer.HasCancellation
					? $", {tokenText}"
					: String.Empty;

				builder.WriteLine();
				builder.WriteLine($"return {awaitPrefix}{deserializer.Name}(response{cancellationTokenSuffix});");

				return;
			}
		}

		foreach (var deserializer in responseDeserializers)
		{
			if (deserializer is { Type.IsGeneric: true })
			{
				var awaitPrefix = deserializer.IsAsync
					? "await "
					: String.Empty;

				var cancellationTokenSuffix = deserializer.HasCancellation
					? $", {tokenText}"
					: String.Empty;

				builder.WriteLine();
				builder.WriteLine($"return {awaitPrefix}{deserializer.Name}<{method.ReturnType.Name}>(response{cancellationTokenSuffix});");


				return;
			}
		}

		builder.WriteLine();

		if (method.ReturnType.IsType<string>())
		{
			builder.WriteLine($"return await response.Content.ReadAsStringAsync({tokenText});");
		}
		else if (method.ReturnType.IsType<byte[]>())
		{
			builder.WriteLine($"return await response.Content.ReadAsByteArrayAsync({tokenText});");
		}
		else if (method.ReturnType.IsType<Stream>())
		{
			builder.WriteLine($"return await response.Content.ReadAsStreamAsync({tokenText});");
		}
		else
		{
			builder.WriteLine($"return await response.Content.ReadFromJsonAsync<{method.ReturnTypeName}>({tokenText});");
		}
	}

	private static void WriteClassEnd(SourceWriter builder)
	{
		builder.Indentation = Math.Max(builder.Indentation - 1, 0);

		builder.WriteLine('}');
	}
}