using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RestBuilder.Helpers;

namespace RestBuilder.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ResponseDeserializerAnalyzer : DiagnosticAnalyzer
{
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
		= ImmutableArray.Create(
			DiagnosticsDescriptors.InvalidUseOfCancellationToken,
			DiagnosticsDescriptors.FirstParameterMustBe,
			DiagnosticsDescriptors.MustHaveXGenericParameters,
			DiagnosticsDescriptors.MustReturnGenericTypeOrAwaitable,
			DiagnosticsDescriptors.XWillNotBeUsed);

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
	}

	private void AnalyzeMethod(SymbolAnalysisContext context)
	{
		// Check if the symbol is an IMethodSymbol, if not, exit the method
		if (context.Symbol is not IMethodSymbol method)
		{
			return;
		}

		// Check if the method's containing type has the RestClientAttribute, if not, exit the method
		if (!method!.ContainingType.HasAttribute(nameof(Literals.RestClientAttribute)))
		{
			return;
		}

		// Initialize attributeIndex and index
		var attributeIndex = -1;
		var index = 0;

		// Iterate over the method's attributes
		foreach (var attribute in method.GetAttributes())
		{
			// If the attribute is ResponseDeserializerAttribute, set attributeIndex to the current index and break the loop
			if (attribute.AttributeClass is { Name: "ResponseDeserializerAttribute", ContainingNamespace: { Name: "RestBuilder" } })
			{
				attributeIndex = index;
				break;
			}

			index++;
		}

		// If the method does not have the ResponseDeserializerAttribute, exit the method
		if (!method.HasAttribute("ResponseDeserializerAttribute"))
		{
			return;
		}

		// If the method is not generic, report a diagnostic that it must have 1 generic parameter
		if (!method.IsGenericMethod)
		{
			context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.Identifier,
				DiagnosticsDescriptors.MustHaveXGenericParameters, 1);
		}
		// If the method is generic but does not have exactly 1 type argument, report a diagnostic
		else if (method.TypeArguments.Length != 1)
		{
			context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.TypeParameterList,
				DiagnosticsDescriptors.MustHaveXGenericParameters, 1);
		}

		// If the method does not have any parameters, report a diagnostic that the first parameter must be HttpResponseMessage
		if (method.Parameters.Length == 0)
		{
			context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.Identifier,
				DiagnosticsDescriptors.FirstParameterMustBe, nameof(HttpResponseMessage));
		}
		// If the method's first parameter is not of type HttpResponseMessage, report a diagnostic
		else if (!method.Parameters[0].Type.IsType<HttpResponseMessage>())
		{
			context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.ParameterList.Parameters[0],
				DiagnosticsDescriptors.FirstParameterMustBe, nameof(HttpResponseMessage));
		}

		// If the method's return type is not awaitable and the second parameter is CancellationToken, report a diagnostic
		if (!method.ReturnType.IsAwaitableType() && method.Parameters[1].Type.IsType<CancellationToken>())
		{
			context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.ParameterList.Parameters[1],
				DiagnosticsDescriptors.InvalidUseOfCancellationToken);
		}

		// If the method's return type does not match its type argument and the return type of the awaitable does not match the type argument, report a diagnostic
		if (!SymbolEqualityComparer.Default.Equals(method.ReturnType, method.TypeArguments[0]) &&
		    !SymbolEqualityComparer.Default.Equals(method.ReturnType.GetAwaitableReturnType(), method.TypeArguments[0]))
		{
			context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.ReturnType,
				DiagnosticsDescriptors.MustReturnGenericTypeOrAwaitable, method.TypeArguments[0].ToDisplayString());
		}

		// Check if any of the methods in the containing type are partial and have a return type that is not void
		var parentHasBodies = method.ContainingType
			.GetMembers()
			.OfType<IMethodSymbol>()
			.Any(m => m.IsPartial() && m.ReturnType.GetAwaitableReturnType().SpecialType != SpecialType.System_Void);

		// If no such methods exist, report a diagnostic that the ResponseDeserializer will not be used
		if (!parentHasBodies)
		{
			context.ReportDiagnostic<MethodDeclarationSyntax>(method, n => n.AttributeLists[attributeIndex],
				DiagnosticsDescriptors.XWillNotBeUsed, "ResponseDeserializer", "no endpoint has a return type");
		}
	}
}