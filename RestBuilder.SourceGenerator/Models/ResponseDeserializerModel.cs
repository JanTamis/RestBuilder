namespace RestBuilder.SourceGenerator.Models;

public record ResponseDeserializerModel
{
	public string Name { get; set; }
	public bool IsAsync { get; set; }
	public bool HasCancellation { get; set; }
	
	public TypeModel? Type { get; set; }
}