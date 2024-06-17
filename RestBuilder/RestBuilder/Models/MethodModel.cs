using System.Net.Http;
using TypeShape.Roslyn;

namespace RestBuilder.Models;

public record MethodModel
{
	public string Name { get; set; }
	public string Path { get; set; }
	public string ReturnType { get; set; }
	public string ReturnNamespace { get; set; }
	
	public bool AllowAnyStatusCode { get; set; }
	
	public HttpMethod Method { get; set; }
	
	public ImmutableEquatableArray<ParameterModel> Parameters { get; set; }
	public ImmutableEquatableArray<LocationAttributeModel> Locations { get; set; }
}