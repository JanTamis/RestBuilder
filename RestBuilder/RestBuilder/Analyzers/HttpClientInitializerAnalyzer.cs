using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RestBuilder.Helpers;
using System.Collections.Immutable;
using System.Net.Http;

namespace RestBuilder.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class HttpClientInitializerAnalyzer : DiagnosticAnalyzer
{
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
		= ImmutableArray.Create(
			DiagnosticsDescriptors.MethodMustReturnType,
			DiagnosticsDescriptors.MethodNoParametersHttpClientInitializer,
			DiagnosticsDescriptors.MethodMustBeStaticHttpClientInitializer);

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
	}

	private void AnalyzeMethod(SymbolAnalysisContext context)
	{
		// Analyzes the method symbol from the context.
		if (context.Symbol is not IMethodSymbol method)
		{
			// If the symbol is not a method, the analysis is stopped.
			return;
		}

		// Checks if the containing type of the method has the RestClientAttribute.
		if (!method!.ContainingType.HasAttribute(nameof(Literals.RestClientAttribute)))
		{
			// If the RestClientAttribute is not present, the analysis is stopped.
			return;
		}

		// Checks if the method has the HttpClientInitializerAttribute.
		if (!method.HasAttribute(nameof(Literals.HttpClientInitializerAttribute)))
		{
			// If the HttpClientInitializerAttribute is not present, the analysis is stopped.
			return;
		}

		// Checks if the method has a return type of HttpClient.
		if (!method.HasReturnType<HttpClient>(context.Compilation))
		{
			// If the method does not return a HttpClient, a diagnostic is reported.
			context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.ReturnType,
				DiagnosticsDescriptors.MethodMustReturnType, nameof(HttpClient));
		}

		// Checks if the method has any parameters.
		if (!method.Parameters.IsEmpty)
		{
			// If the method has parameters, a diagnostic is reported.
			context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.ParameterList,
				DiagnosticsDescriptors.MethodNoParametersHttpClientInitializer);
		}

		// Checks if the method is static.
		if (!method.IsStatic)
		{
			// If the method is not static, a diagnostic is reported.
			context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.Identifier,
				DiagnosticsDescriptors.MethodMustBeStaticHttpClientInitializer);
		}
	}
}