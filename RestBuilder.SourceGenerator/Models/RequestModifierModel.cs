namespace RestBuilder.SourceGenerator.Models;

public record RequestModifierModel
{
	public bool HasCancellation { get; set; }
	public bool IsAsync { get; set; }

	public string Name { get; set; }
}

public record RequestQueryParamSerializerModel
{
	public bool HasFormat { get; set; }
	
	public bool HasFormatProvider { get; set; }
	
	public bool IsCollection { get; set; }
	
	public string Name { get; set; }
	
	public bool IsAsync { get; set; }
	public TypeModel ValueType { get; set; }
}