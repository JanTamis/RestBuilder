namespace RestBuilder.Models;

public record RequestBodySerializerModel
{
	public bool HasCancellation { get; set; }
	public bool IsAsync { get; set; }

	public string Name { get; set; }
}