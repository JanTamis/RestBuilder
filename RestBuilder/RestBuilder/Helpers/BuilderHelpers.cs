using System;
using TypeShape.Roslyn;

namespace RestBuilder.Helpers;

public static class BuilderHelpers
{
	public static IDisposable AppendIndentation(this SourceWriter builder, string head)
	{
		builder.WriteLine(head);
		builder.WriteLine("{");
		builder.Indentation++;

		return new BuilderIndenter(builder, "}");
	}
}

public class BuilderIndenter(SourceWriter builder, string? endText) : IDisposable
{
	public void Dispose()
	{
		builder.Indentation--;

		if (!String.IsNullOrWhiteSpace(endText))
		{
			builder.WriteLine(endText!); 
		}
	}
}
