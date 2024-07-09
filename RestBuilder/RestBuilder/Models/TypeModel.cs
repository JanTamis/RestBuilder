using Microsoft.CodeAnalysis;
using RestBuilder.Interfaces;

namespace RestBuilder.Models;

public record TypeModel : IType
{
	public string Type { get; set; }
	public string Name { get; set; }
	public string Namespace { get; set; }
	
	public LocationAttributeModel? Location { get; set; }

	public bool IsNullable { get; set; }

	public NullableAnnotation NullableAnnotation { get; set; }

	public bool IsCollection { get; set; }
	public bool IsGeneric { get; set; }
	
	public TypeModel? CollectionType { get; set; }
}