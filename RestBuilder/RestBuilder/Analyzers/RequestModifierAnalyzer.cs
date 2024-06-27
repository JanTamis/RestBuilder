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
			 DiagnosticsDescriptors.UseOfCancellationTokenInvalid);

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

		if (!method.HasAttribute(nameof(Literals.RequestModifier)))
		{
			return;
		}

		if (method.ReturnsVoid && method.HasParameters<HttpRequestMessage, CancellationToken>())
		{
			context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.FindAttributeByName(nameof(Literals.RequestModifier)), 
				DiagnosticsDescriptors.UseOfCancellationTokenInvalid);
		}
	}
}
