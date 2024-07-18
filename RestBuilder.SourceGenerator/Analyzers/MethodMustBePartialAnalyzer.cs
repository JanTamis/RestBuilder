using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using RestBuilder.SourceGenerator.Helpers;
using System.Collections.Immutable;
using System.Linq;
using RestBuilder.SourceGenerator.Parsers;
using RestBuilder.Core.Attributes;

namespace RestBuilder.SourceGenerator.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MethodMustBePartialAnalyzer : DiagnosticAnalyzer
{
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = 
		[
			DiagnosticsDescriptors.MethodMustBePartial
		];

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

		// Get the HTTP method of the method symbol using the ClassParser.
		var httpMethod = ClassParser.GetHttpMethod(method!);

		// If the method's containing type does not have the 'RestClientAttribute', return early.
		if (!method!.ContainingType.HasAttribute<RestClientAttribute>(context.Compilation))
		{
			return;
		}

		// If the HTTP method is null, return early.
		if (httpMethod is null)
		{
			return;
		}

		// Iterate over each syntax reference in the method's declaring syntax references.
		foreach (var syntaxReference in method.DeclaringSyntaxReferences)
		{
			// Get the syntax from the syntax reference.
			var syntax = syntaxReference.GetSyntax(context.CancellationToken);

			// If the syntax is not a method declaration or if it has a 'partial' modifier, continue to the next iteration.
			if (syntax is not MethodDeclarationSyntax methodDeclaration || methodDeclaration.Modifiers
						.Any(modifier => modifier.IsKind(SyntaxKind.PartialKeyword)))
			{
				continue;
			}

			// If the syntax is a method declaration without a 'partial' modifier, report a diagnostic.
			context.ReportDiagnostic(Diagnostic.Create(DiagnosticsDescriptors.MethodMustBePartial, methodDeclaration.Identifier.GetLocation(), method.Name));
		}
	}
}