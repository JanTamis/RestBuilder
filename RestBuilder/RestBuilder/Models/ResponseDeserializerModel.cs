namespace RestBuilder.Models;

public record ResponseDeserializerModel
{
	public bool HasStringContent { get; set; }
	
	public string Name { get; set; }
	public bool IsAsync { get; set; }
	public bool HasCancellation { get; set; }
}