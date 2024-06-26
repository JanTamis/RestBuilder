using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using RestBuilder.Helpers;
using System;
using System.Collections.Immutable;
using System.Linq;
using RestBuilder.Parsers;

namespace RestBuilder.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MethodMustBePartialAnalyzer : DiagnosticAnalyzer
{
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
		 = ImmutableArray.Create(DiagnosticsDescriptors.MethodMustBePartial);

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

		foreach (var syntaxReference in method.DeclaringSyntaxReferences)
		{
			var syntax = syntaxReference.GetSyntax(context.CancellationToken);

			if (syntax is not MethodDeclarationSyntax methodDeclaration || methodDeclaration.Modifiers
				.Any(modifier => modifier.IsKind(SyntaxKind.PartialKeyword)))
			{
				continue;
			}

			context.ReportDiagnostic(Diagnostic.Create(DiagnosticsDescriptors.MethodMustBePartial, methodDeclaration.Identifier.GetLocation(), method.Name));
		}
	}
}
