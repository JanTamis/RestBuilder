using RestBuilder.Interfaces;

namespace RestBuilder.Models;

public record ParameterModel : IType
{
	public string Type { get; set; }
	public string Name { get; set; }
	
	public string Namespace { get; set; }
	
	public LocationAttributeModel Location { get; set; }
	
	public bool IsNullable { get; set; }
}