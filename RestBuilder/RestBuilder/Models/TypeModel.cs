using Microsoft.CodeAnalysis;

namespace RestBuilder.Models;

public record TypeModel
{
	public string Type { get; set; }
	public string Name { get; set; }
	public string Namespace { get; set; }
	
	public bool IsNullable { get; set; }

	public NullableAnnotation NullableAnnotation { get; set; }

	public bool IsCollection { get; set; }
	
	public TypeModel? CollectionType { get; set; }
}