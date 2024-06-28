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
		if (context.Symbol is not IMethodSymbol method)
		{
			return;
		}
		
		if (!method!.ContainingType.HasAttribute(nameof(Literals.RestClientAttribute)))
		{
			return;
		}

		if (!method.HasAttribute("RequestModifierAttribute"))
		{
			return;
		}

		if (method.Parameters.Length == 0)
		{
			context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.Identifier,
				DiagnosticsDescriptors.FirstParameterMustBe, nameof(HttpRequestMessage));
		}
		else if (!method.Parameters[0].Type.IsType<HttpRequestMessage>())
		{
			context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.ParameterList.Parameters[0],
				DiagnosticsDescriptors.FirstParameterMustBe, nameof(HttpRequestMessage));
		}

		if (method.ReturnsVoid && method.Parameters.Length == 2 && method.Parameters[1].Type.IsType<CancellationToken>())
		{
			context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.ParameterList.Parameters[1], 
				DiagnosticsDescriptors.InvalidUseOfCancellationToken);
		}
	}
}
