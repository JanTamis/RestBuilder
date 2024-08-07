using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RestBuilder.Core.Attributes;
using RestBuilder.SourceGenerator.Helpers;

namespace RestBuilder.SourceGenerator.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class EndPointAnalyzer : DiagnosticAnalyzer
{
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = 
		[
			DiagnosticsDescriptors.XWillNotBeUsed, DiagnosticsDescriptors.XMustImplement
		];

	private static readonly HashSet<string> Attributes =
	[
		"GetAttribute",
		"PostAttribute",
		"PutAttribute",
		"DeleteAttribute",
		"PatchAttribute",
		"HeadAttribute",
		"OptionsAttribute",
		"TraceAttribute"
	];

	private static readonly HashSet<string> HttpParameterTypes =
	[
		nameof(BodyAttribute),
		nameof(HeaderAttribute),
		nameof(QueryAttribute),
		nameof(QueryMapAttribute),
		nameof(PathAttribute),
	];

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
	}

	private void AnalyzeMethod(SymbolAnalysisContext context)
	{
		// Checks if the symbol is not an IMethodSymbol. If it's not, the method returns.
		if (context.Symbol is not IMethodSymbol method)
		{
			return;
		}

		// Checks if the method's containing type does not have the RestClientAttribute. 
		// If it doesn't, the method returns.
		if (!method!.ContainingType.HasAttribute<RestClientAttribute>(context.Compilation))
		{
			return;
		}

		// Checks if the method does not have an attribute whose class is in the "RestBuilder" namespace 
		// and whose name is contained in the Attributes set. If it doesn't, the method returns.
		if (!method.HasAttribute(h => h.AttributeClass.InheritsFrom<RequestAttribute>(context.Compilation)))
		{
			return;
		}

		// Iterates over each parameter in the method.
		foreach (var parameter in method.Parameters)
		{
			// Checks if the parameter's type is not CancellationToken and if the parameter does not have an attribute 
			// whose class is not CancellationToken, is in the "RestBuilder" namespace, and whose name is contained in the HttpParameterTypes set.
			// If these conditions are met, a diagnostic is reported for the parameter, indicating that the location of the parameter is not defined by an attribute.
			if (!parameter.Type.IsType<CancellationToken>(context.Compilation) &&
					!parameter.HasAttribute(h => h.AttributeClass is not null 
						&& !h.AttributeClass.IsType<CancellationToken>(context.Compilation) 
						&& h.AttributeClass.ContainingNamespace?.ToString() == "RestBuilder.Core.Attributes" 
						&& HttpParameterTypes.Contains(h.AttributeClass.Name)))
			{
				context.ReportDiagnostic<ParameterSyntax>(parameter, n => n.Identifier,
					DiagnosticsDescriptors.XWillNotBeUsed, parameter.Name, "the location of the parameter is not defined by an attribute");
			}

			if (parameter.HasAttribute(nameof(QueryMapAttribute)) && !parameter.Type.Implements("System.Collections.Generic.IDictionary<TKey, TValue>"))
			{
				context.ReportDiagnostic<ParameterSyntax>(parameter, n => n.Type,
					DiagnosticsDescriptors.XMustImplement, "IDictionary<TKey, TValue>");
			}
		}
	}
}