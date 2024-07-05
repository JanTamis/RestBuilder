using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RestBuilder.Helpers;

namespace RestBuilder.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class RestClientAnalyzer : DiagnosticAnalyzer
{
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
		= ImmutableArray.Create(DiagnosticsDescriptors.XWillNotBeUsed,
			DiagnosticsDescriptors.InvalidUrl);

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.NamedType);
	}

	private void AnalyzeMethod(SymbolAnalysisContext context)
	{
		// Check if the symbol from the context is not an instance of INamedTypeSymbol.
		// If it's not, exit the method early.
		if (context.Symbol is not INamedTypeSymbol type)
		{
			return;
		}

		// Check if the type does not have the "RestClientAttribute" attribute.
		// If it doesn't, exit the method early.
		if (!type.HasAttribute("RestClientAttribute"))
		{
			return;
		}

		// Check if the type has any method with the "HttpClientInitializerAttribute" attribute.
		var hasHttpClientInitializer = type
			.GetMembers()
			.OfType<IMethodSymbol>()
			.Any(w => w.HasAttribute("HttpClientInitializerAttribute"));

		var attributes = type.GetAttributes();

		for (var i = 0; i < attributes.Length; i++)
		{
			var attribute = attributes[i];

			// If the attribute is from the "RestBuilder" namespace and its name is "BaseAddressAttribute",
			// report a diagnostic message.
			if (attribute.AttributeClass?.ContainingNamespace?.ToString() == Literals.BaseNamespace &&
			    attribute.AttributeClass.Name == nameof(Literals.BaseAddressAttribute))
			{
				var path = type
					.GetAttribute(nameof(Literals.BaseAddressAttribute))
					.GetValue(0, String.Empty);
				
				if (hasHttpClientInitializer)
				{
					context.ReportDiagnostic<TypeDeclarationSyntax>(type, n => n.AttributeLists.Count > i ? n.AttributeLists[i] : null,
						DiagnosticsDescriptors.XWillNotBeUsed, attribute.AttributeClass.Name.Replace("Attribute", String.Empty), "HttpClientInitializer is being used");
				}

				if (!Uri.TryCreate(path, UriKind.Absolute, out var uriResult) || 
				    uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps)
				{
					context.ReportDiagnostic<TypeDeclarationSyntax>(type, n => n.AttributeLists.Count > i ? n.AttributeLists[i] : null,
						DiagnosticsDescriptors.InvalidUrl, attribute.AttributeClass.Name.Replace("Attribute", String.Empty));
				}
			}
		}
	}
}