using System;
using Microsoft.CodeAnalysis;
using RestBuilder.SourceGenerator.Interfaces;
using TypeShape.Roslyn;

namespace RestBuilder.SourceGenerator.Helpers;

public static class BuilderHelpers
{
	public static IDisposable AppendIndentation(this SourceWriter builder, string head)
	{
		builder.WriteLine(head);
		builder.WriteLine("{");
		builder.Indentation++;

		return new BuilderIndenter(builder, "}");
	}

	public static IDisposable AppendIndentationWithCondition(this SourceWriter builder, string head, Func<bool> condition)
	{
		if (condition())
		{
			builder.WriteLine(head);
			builder.WriteLine("{");
			builder.Indentation++;
		}

		return new BuilderIndenterWithCondition(builder, "}", condition);
	}

	public static IDisposable AppendnullCheck(this SourceWriter builder, IType type)
	{
		var condition = () => type is { IsNullable: true, NullableAnnotation: NullableAnnotation.Annotated };
		
		if (condition())
		{
			builder.WriteLine();
			builder.WriteLine($"if ({type.Name} != null)");
			builder.WriteLine("{");
			builder.Indentation++;
		}

		return new BuilderIndenterWithCondition(builder, "}", condition);
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


public class BuilderIndenterWithCondition(SourceWriter builder, string? endText, Func<bool> condition) : IDisposable
{
	public void Dispose()
	{
		if (condition())
		{
			builder.Indentation--;

			if (!String.IsNullOrWhiteSpace(endText))
			{
				builder.WriteLine(endText!);
			}
		}
		
	}
}
