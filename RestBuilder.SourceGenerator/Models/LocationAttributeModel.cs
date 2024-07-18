using RestBuilder.SourceGenerator.Enumerators;

namespace RestBuilder.SourceGenerator.Models;

public record LocationAttributeModel
{
	public string? Name { get; set; }

	public string? Value { get; set; }

	public string? Format { get; set; }

	public bool UrlEncode { get; set; }

	public HttpLocation Location { get; set; }
}
