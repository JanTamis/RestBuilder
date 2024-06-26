using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using RestBuilder.Helpers;
using RestBuilder.Parsers;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace RestBuilder.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MethodMustReturnAwaitableAnalyzer : DiagnosticAnalyzer
{
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
		 = ImmutableArray.Create(DiagnosticsDescriptors.MethodMustReturnAwaitable);

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
	}

	private void AnalyzeMethod(SymbolAnalysisContext context)
	{
		var method = context.Symbol as IMethodSymbol;
		var httpMethod = ClassParser.GetHttpMethod(method!);

		if (!method!.ContainingType.HasAttribute(nameof(Literals.RestClientAttribute)))
		{
			return;
		}

		if (httpMethod is null)
		{
			return;
		}

		if (method.ReturnType.IsAwaitableType())
		{
			return;
		}

		context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.ReturnType, DiagnosticsDescriptors.MethodMustReturnAwaitable);
	}
}
