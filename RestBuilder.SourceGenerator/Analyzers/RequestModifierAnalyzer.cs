using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using RestBuilder.SourceGenerator.Helpers;
using System.Collections.Immutable;
using System.Net.Http;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RestBuilder.Core.Attributes;

namespace RestBuilder.SourceGenerator.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class RequestModifierAnalyzer : DiagnosticAnalyzer
{
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = 
		[
			DiagnosticsDescriptors.InvalidUseOfCancellationToken, 
			DiagnosticsDescriptors.FirstParameterMustBe
		];

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
	}

	private void AnalyzeMethod(SymbolAnalysisContext context)
	{
		// Check if the symbol is an IMethodSymbol, if not, exit the function.
		if (context.Symbol is not IMethodSymbol method)
		{
			return;
		}

		// Check if the method's containing type has the `RestClientAttribute` attribute.
		// If it doesn't, exit the function.
		if (!method!.ContainingType.HasAttribute<RestClientAttribute>(context.Compilation))
		{
			return;
		}

		// Check if the method has the `RequestModifierAttribute` attribute.
		// If it doesn't, exit the function.
		if (!method.HasAttribute<RequestModifierAttribute>(context.Compilation))
		{
			return;
		}

		// Check if the method has any parameters.
		// If it doesn't, report a diagnostic that the first parameter must be of type `HttpRequestMessage`.
		if (method.Parameters.Length == 0)
		{
			context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.Identifier,
				DiagnosticsDescriptors.FirstParameterMustBe, nameof(HttpRequestMessage));
		}
		// If the method has parameters, check if the first parameter is of type `HttpRequestMessage`.
		// If it's not, report a diagnostic that the first parameter must be of type `HttpRequestMessage`.
		else if (!method.Parameters[0].Type.IsType<HttpRequestMessage>(context.Compilation))
		{
			context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.ParameterList.Parameters[0],
				DiagnosticsDescriptors.FirstParameterMustBe, nameof(HttpRequestMessage));
		}

		// Check if the method returns void and has exactly two parameters, and if the second parameter is of type `CancellationToken`.
		// If these conditions are met, report a diagnostic that the use of `CancellationToken` is invalid.
		if (method is { ReturnsVoid: true, Parameters.Length: 2 } && method.Parameters[1].Type.IsType<CancellationToken>(context.Compilation))
		{
			context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.ParameterList.Parameters[1],
				DiagnosticsDescriptors.InvalidUseOfCancellationToken);
		}
	}
}