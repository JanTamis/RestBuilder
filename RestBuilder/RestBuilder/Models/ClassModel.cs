using TypeShape.Roslyn;

namespace RestBuilder.Models;

public record ClassModel
{
	public string Name { get; set; }
	public string Namespace { get; set; }
	public string BaseAddress { get; set; }
	
	public bool IsStatic { get; set; }
	
	public ImmutableEquatableArray<MethodModel> Methods { get; set; }
}