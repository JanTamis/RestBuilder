using Microsoft.CodeAnalysis;

namespace RestBuilder.SourceGenerator.Analyzers;

public static class DiagnosticsDescriptors
{
	public static readonly DiagnosticDescriptor MethodMustBePartial = new(
		"REST001",
		"Method must be partial",
		"The method '{0}' must be partial",
		"RestAnalyzer",
		DiagnosticSeverity.Error,
		true);

	public static readonly DiagnosticDescriptor MethodMustReturnAwaitable = new(
		"REST002",
		"Method must return an awaitable type",
		"The method '{0}' must return an awaitable type",
		"RestAnalyzer",
		DiagnosticSeverity.Error,
		true);

	public static readonly DiagnosticDescriptor MethodMustReturnType = new(
		"REST003",
		"Method must return {1}",
		"The method '{0}' must return {1}",
		"RestAnalyzer",
		DiagnosticSeverity.Error,
		true);

	public static readonly DiagnosticDescriptor MethodNoParametersHttpClientInitializer = new(
		"REST004",
		"Method cannot contain parameters",
		"The method '{0}' cannot contain any parameters",
		"RestAnalyzer",
		DiagnosticSeverity.Error,
		true);

	public static readonly DiagnosticDescriptor MethodMustBeStaticHttpClientInitializer = new(
		"REST005",
		"Method must be static",
		"The method '{0}' must be static",
		"RestAnalyzer",
		DiagnosticSeverity.Error,
		true);

	public static readonly DiagnosticDescriptor InvalidUseOfCancellationToken = new(
		"REST006",
		"Invalid use of the CancellationToken",
		"A CancellationToken should not be provided if '{0}' does not return an awaitable type",
		"RestAnalyzer",
		DiagnosticSeverity.Warning,
		true);

	public static readonly DiagnosticDescriptor FirstParameterMustBe = new(
		"REST007",
		"Invalid first parameter",
		"The first parameter of '{0}' must be {1}",
		"RestAnalyzer",
		DiagnosticSeverity.Error,
	true);

	public static readonly DiagnosticDescriptor MustHaveXGenericParameters = new(
		"REST008",
		"Method must have {0} generic parameters",
		"The method '{0}' must have {1} generic parameters",
		"RestAnalyzer",
		DiagnosticSeverity.Error,
		true);

	public static readonly DiagnosticDescriptor MustReturnGenericTypeOrAwaitable = new(
		"REST009",
		"The method '{0}' must return {1} or an awaitable that returns {1}",
		"The method '{0}' must return {1} or an awaitable type that returns {1}",
		"RestAnalyzer",
		DiagnosticSeverity.Error,
		true);

	public static readonly DiagnosticDescriptor XWillNotBeUsed = new(
		"REST010",
		"'{1}' will not be used because {2}",
		"'{1}' will not be used because {2}",
		"RestAnalyzer",
		DiagnosticSeverity.Warning,
		true);

	public static readonly DiagnosticDescriptor XMustImplement = new(
		"REST011",
		"'{0}' must implement {1}",
		"'{0}' must be an {1}",
		"RestAnalyzer",
		DiagnosticSeverity.Error,
		true);

	public static readonly DiagnosticDescriptor InvalidUrl = new(
		"REST012",
		"'{1}' should use a valid URL",
		"'{1}' should use a valid URL",
		"RestAnalyzer",
		DiagnosticSeverity.Warning,
		true);
}
