using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RestBuilder.Core.Attributes;
using RestBuilder.SourceGenerator.Helpers;

namespace RestBuilder.SourceGenerator.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class RequestBodySerializer : DiagnosticAnalyzer
{
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
		= ImmutableArray.Create(
			DiagnosticsDescriptors.MustHaveXGenericParameters,
			DiagnosticsDescriptors.MethodMustReturnType,
			DiagnosticsDescriptors.XWillNotBeUsed,
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
		// Check if the symbol is a method, if not, return.
		if (context.Symbol is not IMethodSymbol method)
		{
			return;
		}

		// Check if the containing type of the method has a RestClientAttribute, if not, return.
		if (!method!.ContainingType.HasAttribute<RestClientAttribute>(context.Compilation))
		{
			return;
		}

		// Initialize attribute index and counter.
		var attributeIndex = -1;
		var index = 0;

		// Iterate over the attributes of the method.
		foreach (var attribute in method.GetAttributes())
		{
			// If the attribute is of type RequestBodySerializerAttribute and is in the RestBuilder namespace, store the index and break the loop.
			if (attribute.AttributeClass.IsType<RequestBodySerializerAttribute>(context.Compilation))
			{
				attributeIndex = index;
				break;
			}

			index++;
		}

		// If the method does not have a RequestBodySerializerAttribute, return.
		if (!method.HasAttribute<RequestBodySerializerAttribute>(context.Compilation))
		{
			return;
		}

		// If the method is not generic, report a diagnostic that it must have one generic parameter.
		// if (!method.IsGenericMethod)
		// {
		// 	context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.Identifier,
		// 		DiagnosticsDescriptors.MustHaveXGenericParameters, 1);
		// }
		// If the method is generic but does not have exactly one type argument, report a diagnostic.
		if (method.IsGenericMethod && method.TypeArguments.Length != 1)
		{
			context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.TypeParameterList,
				DiagnosticsDescriptors.MustHaveXGenericParameters, 1);
		}

		// If the method does not return HttpContent or a task of HttpContent, report a diagnostic.
		if (!method.ReturnType.IsType<HttpContent>(context.Compilation) && !method.ReturnType.GetAwaitableReturnType().IsType<HttpContent>(context.Compilation))
		{
			context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.ReturnType,
				DiagnosticsDescriptors.MethodMustReturnType, nameof(HttpContent));
		}

		// If the method has two parameters and the second one is a CancellationToken, report a diagnostic.
		if (method.Parameters.Length == 2 && method.Parameters[1].Type.IsType<CancellationToken>(context.Compilation))
		{
			context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.ParameterList.Parameters[1],
				DiagnosticsDescriptors.InvalidUseOfCancellationToken);
		}

		// If the method has type arguments and parameters, and the type of the first parameter does not match the first type argument, report a diagnostic.
		if (method.TypeArguments.Length > 0 && method.Parameters.Length > 0 && !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, method.TypeArguments[0]))
		{
			context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.ParameterList.Parameters[0],
				DiagnosticsDescriptors.FirstParameterMustBe, method.TypeArguments[0].Name);
		}

		// Check if the containing type of the method has any partial methods with a BodyAttribute.
		var parentHasBodies = method.ContainingType.GetMembers()
			.OfType<IMethodSymbol>()
			.Any(m => m.IsPartial() && m.Parameters.Any(a => a.HasAttribute<BodyAttribute>(context.Compilation)));

		// If no such methods are found, report a diagnostic that the RequestBodySerializer will not be used.
		if (!parentHasBodies)
		{
			context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.AttributeLists[attributeIndex],
				DiagnosticsDescriptors.XWillNotBeUsed, "RequestBodySerializer", "no method has a body parameter");
		}
	}
}