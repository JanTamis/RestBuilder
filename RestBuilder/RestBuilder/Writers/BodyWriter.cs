﻿using RestBuilder.Helpers;
using RestBuilder.Interfaces;
using RestBuilder.Models;
using System;
using System.IO;
using System.Net.Http;
using TypeShape.Roslyn;

namespace RestBuilder.Writers;

public static class BodyWriter
{
	public static void WriteRequestBody(IType body, ClassModel classModel, string tokenText, SourceWriter builder)
	{
		if (classModel.RequestBodySerializer is not null)
		{
			var awaitPrefix = classModel.RequestBodySerializer.IsAsync
				? "await "
				: String.Empty;

			var cancellationTokenSuffix = classModel.RequestBodySerializer.HasCancellation
				? $", {tokenText}"
				: String.Empty;

			builder.WriteLine($"request.Content = {awaitPrefix}{classModel.RequestBodySerializer.Name}({body.Name}{cancellationTokenSuffix});");
		}
		else if (body.IsType<string>())
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
}
