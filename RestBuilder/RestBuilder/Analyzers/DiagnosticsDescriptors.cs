using Microsoft.CodeAnalysis;

namespace RestBuilder.Analyzers;

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
		"Method must return a awaitable type",
		"The method '{0}' must return a awaitable type",
		"RestAnalyzer",
		DiagnosticSeverity.Error,
		true);

	public static readonly DiagnosticDescriptor MethodMustReturnHttpClientHttpClientInitializer = new(
		"REST003",
		"Method must return HttpClient",
		"The method '{0}' must return HttpClient",
		"RestAnalyzer",
		DiagnosticSeverity.Error,
		true);

	public static readonly DiagnosticDescriptor MethodNoParametersHttpClientInitializer = new(
		"REST004",
		"Method must not contain parameters",
		"The method '{0}' must not contain parameters",
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
}
