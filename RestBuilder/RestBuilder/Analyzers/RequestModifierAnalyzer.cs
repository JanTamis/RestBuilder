using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using RestBuilder.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net.Http;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RestBuilder.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class RequestModifierAnalyzer : DiagnosticAnalyzer
{
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
		= ImmutableArray.Create(
			DiagnosticsDescriptors.InvalidUseOfCancellationToken,
			DiagnosticsDescriptors.FirstParameterMustBe);

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
		if (!method!.ContainingType.HasAttribute(nameof(Literals.RestClientAttribute)))
		{
			return;
		}

		// Check if the method has the `RequestModifierAttribute` attribute.
		// If it doesn't, exit the function.
		if (!method.HasAttribute(nameof(Literals.RequestModifierAttribute)))
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
		else if (!method.Parameters[0].Type.IsType<HttpRequestMessage>())
		{
			context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.ParameterList.Parameters[0],
				DiagnosticsDescriptors.FirstParameterMustBe, nameof(HttpRequestMessage));
		}

		// Check if the method returns void and has exactly two parameters, and if the second parameter is of type `CancellationToken`.
		// If these conditions are met, report a diagnostic that the use of `CancellationToken` is invalid.
		if (method is { ReturnsVoid: true, Parameters.Length: 2 } && method.Parameters[1].Type.IsType<CancellationToken>())
		{
			context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.ParameterList.Parameters[1],
				DiagnosticsDescriptors.InvalidUseOfCancellationToken);
		}
	}
}