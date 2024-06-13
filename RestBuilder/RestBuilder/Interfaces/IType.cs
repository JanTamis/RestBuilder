using Microsoft.CodeAnalysis;
using RestBuilder.Models;

namespace RestBuilder.Interfaces;

public interface IType
{
	string Type { get; set; }
	string Name { get; set; }

	string Namespace { get; set; }

	LocationAttributeModel Location { get; set; }

	NullableAnnotation NullableAnnotation { get; set; }

	bool IsNullable { get; set; }
}