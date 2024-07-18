using System.Net.Http;
using TypeShape.Roslyn;

namespace RestBuilder.SourceGenerator.Models;

public record MethodModel
{
	public string Name { get; set; }
	public string Path { get; set; }
	public string ReturnTypeName { get; set; }
	public string ReturnNamespace { get; set; }
	
	public bool AllowAnyStatusCode { get; set; }
	public bool IsAwaitable { get; internal set; }

	public HttpMethod Method { get; set; }
	public TypeModel ReturnType { get; set; }
	
	public ImmutableEquatableArray<ParameterModel> Parameters { get; set; }
	public ImmutableEquatableArray<LocationAttributeModel> Locations { get; set; }	
}