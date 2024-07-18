namespace RestBuilder.SourceGenerator.Models;

public record RequestBodySerializerModel
{
	public bool HasCancellation { get; set; }
	public bool IsAsync { get; set; }

	public string Name { get; set; }
	
	public TypeModel? Type { get; set; }
}