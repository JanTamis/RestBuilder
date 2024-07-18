using Microsoft.CodeAnalysis;
using RestBuilder.SourceGenerator.Models;

namespace RestBuilder.SourceGenerator.Interfaces;

public interface IType
{
	string Type { get; set; }
	string Name { get; set; }

	string Namespace { get; set; }

	LocationAttributeModel Location { get; set; }

	NullableAnnotation NullableAnnotation { get; set; }

	bool IsNullable { get; set; }
}