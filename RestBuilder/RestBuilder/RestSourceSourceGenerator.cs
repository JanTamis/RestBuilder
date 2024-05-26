using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
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
	private static readonly Regex _holeRegex = new Regex(@"\{\d+(:[^}]*)?\}");

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

		var classes = context.SyntaxProvider
			.ForAttributeWithMetadataName(
				$"{Literals.BaseNamespace}.{Literals.BaseAddressAttribute}",
				(node, token) => node is ClassDeclarationSyntax classDeclaration && classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)),
				GenerateSource);

		context.RegisterSourceOutput(classes,
			static (spc, source) => Execute(source, spc));
	}

	private ClassModel GenerateSource(GeneratorAttributeSyntaxContext context, CancellationToken token)
	{
		return ClassParser.Parse(context.TargetNode as ClassDeclarationSyntax, context.TargetSymbol as INamedTypeSymbol, context.Attributes, context.SemanticModel.Compilation);
	}

	private static void Execute(ClassModel source, SourceProductionContext context)
	{
		var builder = new SourceWriter('\t', 1);

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
			])
			.Where(w => !String.IsNullOrEmpty(w))
			.Distinct()
			.OrderBy(o => o)
			.Select(s => $"using {s};");

		builder.WriteLine($$"""
			{{String.Join("\n", namespaces)}}

			namespace {{source.Namespace}};

			public partial class {{source.Name}}
			{
				public HttpClient Client { get; } = new HttpClient() 
				{ 
					BaseAddress = new Uri("{{source.BaseAddress}}"),
				};
			""");

		foreach (var method in source.Methods)
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
				.Concat<IType>(source.Properties)
				.DistinctBy(d => d.Location.Name ?? d.Name);

			if (method.Parameters.Where(w => w.Location.Location != HttpLocation.None).All(a => a.Location.Location is HttpLocation.Query or HttpLocation.Path))
			{
				if (method.ReturnType is nameof(HttpResponseMessage))
				{
					builder.WriteLine($"var response = await Client.{method.Method?.Method ?? "Get"}Async({ParsePath(method.Path, items)}, {tokenText});");
				}
				else
				{
					builder.WriteLine($"using var response = await Client.{method.Method?.Method ?? "Get"}Async({ParsePath(method.Path, items)}, {tokenText});");
				}
			}
			else
			{
				builder.WriteLine($"using var request = new HttpRequestMessage(HttpMethod.{method.Method?.Method ?? "Get"}, {ParsePath(method.Path, items)});");

				var headers = method.Parameters
					.Where(w => w.Location.Location == HttpLocation.Header)
					.Concat<IType>(source.Properties.Where(w => w.Location.Location == HttpLocation.Header))
					.DistinctBy(d => d.Location.Name ?? d.Name)
					.ToLookup(g => g.IsNullable && String.IsNullOrEmpty(g.Location.Format));

				if (headers.Any())
				{
					if (headers[false].Any())
					{
						builder.WriteLine();
					}
					
					foreach (var header in headers[false])
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
					
					foreach (var header in headers[true])
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

					builder.WriteLine();
				}

				if (method.ReturnType is nameof(HttpResponseMessage))
				{
					builder.WriteLine($"var response = await Client.SendAsync(request, {tokenText});");
				}
				else
				{
					builder.WriteLine($"using var response = await Client.SendAsync(request, {tokenText});");
				}
			}


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

			builder.Indentation--;
			builder.WriteLine('}');
		}

		builder.Indentation = Math.Max(builder.Indentation - 1, 0);

		builder.WriteLine('}');

		context.AddSource($"{source.Name}.g.cs", builder.ToSourceText());
	}

	private static string ParsePath(string path, IEnumerable<IType> parameters)
	{
		// TODO only allow holes in path and if path has a hole replace if with a literal
		var hasHoles = parameters.Any(a => a.Location.Location is HttpLocation.Path or HttpLocation.Query);

		path = AddQueryString(path, parameters
			.Where(w => w.Location.Location == HttpLocation.Query && !w.IsNullable)
			.Select(s => new KeyValuePair<string, string>(s.Location.Name ?? s.Name, String.IsNullOrEmpty(s.Location.Format)
				? $"{{{s.Name}}}"
				: $"{{{s.Name}:{s.Location.Format}}}")));

		// foreach (var parameter in parameters)
		// {
		// 	if (parameter.Location == HttpLocation.Path)
		// 	{
		// 		path = path.Replace($"{{{parameter.Name}}}", $"{{{parameter.Name}}}");
		// 	}
		// }

		if (hasHoles)
		{
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
			sb.Append(Uri.EscapeUriString(parameter.Key));
			sb.Append('=');
			sb.Append(parameter.Value);
			hasQuery = true;
		}

		sb.Append(anchorText.ToString());
		return sb.ToString();
	}

	private static bool HasHoles(string value)
	{
		return _holeRegex.IsMatch(value);
	}

	private static string FillHoles(string value, params string[] fieldName)
	{
		return _holeRegex.Replace(value, m =>
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
}