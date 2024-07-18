using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RestBuilder.Core.Attributes;
using RestBuilder.SourceGenerator.Helpers;
using RestBuilder.SourceGenerator.Parsers;
using System.Collections.Immutable;

namespace RestBuilder.SourceGenerator.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MethodMustReturnAwaitableAnalyzer : DiagnosticAnalyzer
{
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = 
		[
			DiagnosticsDescriptors.MethodMustReturnAwaitable
		];

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
	}

	private void AnalyzeMethod(SymbolAnalysisContext context)
	{
		// Check if the symbol is not an instance of IMethodSymbol
		// If it's not, exit the method early
		if (context.Symbol is not IMethodSymbol method)
		{
			return;
		}

		// Get the HTTP method associated with the method symbol
		var httpMethod = ClassParser.GetHttpMethod(method!);

		// Check if the containing type of the method does not have the RestClientAttribute
		// If it doesn't, exit the method early
		if (!method!.ContainingType.HasAttribute<RestClientAttribute>(context.Compilation))
		{
			return;
		}

		// Check if the HTTP method is null
		// If it is, exit the method early
		if (httpMethod is null)
		{
			return;
		}

		// Check if the return type of the method is an awaitable type
		// If it is, exit the method early
		if (method.ReturnType.IsAwaitableType())
		{
			return;
		}

		// If all the checks pass, report a diagnostic that the method must return an awaitable type
		context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.ReturnType, DiagnosticsDescriptors.MethodMustReturnAwaitable);
	}
}