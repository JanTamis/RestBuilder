using TypeShape.Roslyn;

namespace RestBuilder.Models;

public record ClassModel
{
	public string Name { get; set; }
	public string Namespace { get; set; }
	public string BaseAddress { get; set; }
	public string ClientName { get; set; }
	
	public bool IsStatic { get; set; }
	public bool IsDisposable { get; set; }
	
	public ImmutableEquatableArray<MethodModel> Methods { get; set; }
	public ImmutableEquatableArray<PropertyModel> Properties { get; set; }
	
	public ImmutableEquatableArray<LocationAttributeModel> Attributes { get; set; }
}