﻿using RestBuilder.SourceGenerator.Helpers;
using RestBuilder.SourceGenerator.Interfaces;
using RestBuilder.SourceGenerator.Models;
using System;
using System.IO;
using System.Net.Http;
using RestBuilder.SourceGenerator.Parsers;
using TypeShape.Roslyn;

namespace RestBuilder.SourceGenerator.Writers;

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

		builder.WriteLine($"// Set the content of the request");

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

		builder.WriteLine($"// Set the content of the request using {bodySerializer.Name}");
		builder.WriteLine($"request.Content = {awaitPrefix}{bodySerializer.Name}({body.Name}{cancellationTokenSuffix});");
	}
}