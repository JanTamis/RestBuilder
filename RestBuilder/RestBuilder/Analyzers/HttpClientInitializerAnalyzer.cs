using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RestBuilder.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Text;

namespace RestBuilder.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class HttpClientInitializerAnalyzer : DiagnosticAnalyzer
{
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
		 = ImmutableArray.Create(
			 DiagnosticsDescriptors.MethodMustReturnHttpClientHttpClientInitializer, 
			 DiagnosticsDescriptors.MethodNoParametersHttpClientInitializer, 
			 DiagnosticsDescriptors.MethodMustBeStaticHttpClientInitializer);

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
	}

	private void AnalyzeMethod(SymbolAnalysisContext context)
	{
		var method = context.Symbol as IMethodSymbol;

		if (!method!.ContainingType.HasAttribute(nameof(Literals.RestClientAttribute)))
		{
			return;
		}

		if (!method.HasAttribute(nameof(Literals.HttpClientInitializerAttribute)))
		{
			return;
		}
		
		if (!method.HasReturnType<HttpClient>())
		{
			context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.FindAttributeByName("HttpClientInitializer"), 
				DiagnosticsDescriptors.MethodMustReturnHttpClientHttpClientInitializer);
		}

		if (!method.Parameters.IsEmpty)
		{
			context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.FindAttributeByName("HttpClientInitializer"), 
				DiagnosticsDescriptors.MethodNoParametersHttpClientInitializer);
		}

		if (!method.IsStatic)
		{
			context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.FindAttributeByName("HttpClientInitializer"), 
				DiagnosticsDescriptors.MethodMustBeStaticHttpClientInitializer);
		}
	}
}
