using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using RestBuilder.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

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

		if (!method.HasAttribute(nameof(Literals.RequestModifierAttribute)))
		{
			return;
		}


	}
}
