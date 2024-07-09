using RestBuilder.Helpers;
using RestBuilder.Interfaces;
using RestBuilder.Models;
using System;
using System.IO;
using System.Net.Http;
using RestBuilder.Parsers;
using TypeShape.Roslyn;

namespace RestBuilder.Writers;

public static class BodyWriter
{
	public static void WriteRequestBody(IType body, ClassModel classModel, string tokenText, SourceWriter builder)
	{
		foreach (var bodySerializer in classModel.RequestBodySerializers)
		{
			if (bodySerializer is { Type.IsGeneric: false } && ClassParser.TypeEquals(body, bodySerializer.Type))
			{
				AppendSerializer(body, tokenText, builder, bodySerializer);

				return;
			}
		}

		foreach (var bodySerializer in classModel.RequestBodySerializers)
		{
			if (bodySerializer is { Type.IsGeneric: true })
			{
				AppendSerializer(body, tokenText, builder, bodySerializer);

				return;
			}
		}

		if (body.IsType<string>())
		{
			builder.WriteLine($"request.Content = new StringContent({body.Name});");
		}
		else if (body.IsType<byte[]>())
		{
			builder.WriteLine($"request.Content = new ByteArrayContent({body.Name});");
		}
		else if (body.IsType<Stream>())
		{
			builder.WriteLine($"request.Content = new StreamContent({body.Name});");
		}
		else if (body.IsType<HttpContent>())
		{
			builder.WriteLine($"request.Content = {body.Name};");
		}
		else
		{
			builder.WriteLine($"request.Content = JsonContent.Create({body.Name});");
		}
	}
	private static void AppendSerializer(IType body, string tokenText, SourceWriter builder, RequestBodySerializerModel bodySerializer)
	{
		var awaitPrefix = bodySerializer.IsAsync
			? "await "
			: String.Empty;

		var cancellationTokenSuffix = bodySerializer.HasCancellation
			? $", {tokenText}"
			: String.Empty;

		builder.WriteLine($"request.Content = {awaitPrefix}{bodySerializer.Name}({body.Name}{cancellationTokenSuffix});");
	}
}