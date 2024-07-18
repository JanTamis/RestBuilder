using System;
using System.Linq;
using System.Text.RegularExpressions;
using RestBuilder.SourceGenerator.Enumerators;
using RestBuilder.SourceGenerator.Helpers;
using RestBuilder.SourceGenerator.Interfaces;
using RestBuilder.SourceGenerator.Models;
using TypeShape.Roslyn;

namespace RestBuilder.SourceGenerator.Writers;

public static class HeaderWriter
{
	private static readonly Regex HoleRegex = new Regex(@"\{\d+(:[^}]*)?\}");
	
	public static void WriteHeader(IType header, LocationAttributeModel? defaultItem, SourceWriter builder)
	{
		var format = !String.IsNullOrEmpty(header.Location.Format)
			? $"\"{header.Location.Format}\""
			: String.Empty;

		var suffix = header is { Namespace: "System", Type: "String" or "string" }
			? String.Empty
			: $".ToString({format})";

		var result = $"{header.Name}{suffix}";

		if (HasHoles(header.Location.Format))
		{
			result = $"$\"{FillHoles(header.Location.Format, header.Name)}\"";
		}

		if (defaultItem is null)
		{
			builder.WriteLine($"request.Headers.Add(\"{header.Location.Name ?? header.Name}\", {result});");
		}
		else
		{
			builder.WriteLine($"request.Headers.Add(\"{header.Location.Name ?? header.Name}\", {result} ?? \"{defaultItem.Value}\");");
		}
	}

	public static void WriteNullableHeader(IType header, LocationAttributeModel? defaultItems, SourceWriter builder)
	{
		var format = !String.IsNullOrEmpty(header.Location.Format)
			? $"\"{header.Location.Format}\""
			: String.Empty;

		var suffix = header is { Namespace: "System", Type: "String" or "string" }
			? String.Empty
			: $".ToString({format})";

		if (header.IsNullable && defaultItems != null)
		{
			suffix = '?' + suffix;
		}

		var result = $"{header.Name}{suffix}";

		if (HasHoles(header.Location.Format))
		{
			result = $"$\"{FillHoles(header.Location.Format, header.Name)}\"";
		}

		if (defaultItems is null)
		{
			builder.WriteLine();

			using (builder.AppendIndentation($"if ({header.Name} is not null)"))
			{
				builder.WriteLine($"request.Headers.Add(\"{header.Location.Name}\", {result});");
			}
		}
		else
		{
			builder.WriteLine($"request.Headers.Add(\"{header.Location.Name}\", {result} ?? \"{defaultItems.Value}\");");
		}
	}
	
	public static string ParseHeader(LocationAttributeModel location, Func<string, string, string> defaultBuilder)
	{
		switch (location.Name)
		{
			case "Accept":
				return $"Accept.Add(new MediaTypeWithQualityHeaderValue(\"{location.Value}\"))";
			case "Accept-Charset":
				return $"AcceptCharset.Add(new StringWithQualityHeaderValue(\"{location.Value}\"))";
			case "Accept-Encoding":
				return $"AcceptEncoding.Add(new StringWithQualityHeaderValue(\"{location.Value}\"))";
			case "Accept-Language":
				return $"AcceptLanguage.Add(new StringWithQualityHeaderValue(\"{location.Value}\"))";
			case "Authorization":
				var authorizationArguments = location.Value.Split([' '], StringSplitOptions.RemoveEmptyEntries)
					.Select(s => $"\"{s}\"")
					.Take(2);

				return $"Authorization = new AuthenticationHeaderValue({String.Join(", ", authorizationArguments)})";
			case "Connection":
				return $"Connection.Add(\"{location.Value}\")";
			case "ConnectionClose" when Boolean.TryParse(location.Value, out var result):
				return $"ConnectionClose = {result.ToString().ToLower()}";
			case "Date" when DateTime.TryParse(location.Value, out _):
				return $"Date = DateTimeOffset.Parse(\"{location.Value}\")";
			case "Expect":
				return $"Expect.Add(new NameValueWithParametersHeaderValue(\"{location.Value}\"))";
			case "ExpectContinue" when Boolean.TryParse(location.Value, out var result):
				return $"ExpectContinue = {result.ToString().ToLower()}";
			case "From":
				return $"From = \"{location.Value}\"";
			case "Host":
				return $"Host = \"{location.Value}\"";
			case "If-Match":
				return $"IfMatch.Add(new EntityTagHeaderValue(\"{location.Value}\"))";
			case "If-Modified-Since" when DateTime.TryParse(location.Value, out _):
				return $"IfModifiedSince = DateTimeOffset.Parse(\"{location.Value}\")";
			case "If-None-Match":
				return $"IfNoneMatch.Add(new EntityTagHeaderValue(\"{location.Value}\"))";
			case "If-Range":
				return $"IfRange = new RangeConditionHeaderValue(\"{location.Value}\")";
			case "If-Unmodified-Since" when DateTime.TryParse(location.Value, out _):
				return $"IfUnmodifiedSince = DateTimeOffset.Parse(\"{location.Value}\")";
			case "Max-Forwards" when Int32.TryParse(location.Value, out var result):
				return $"MaxForwards = {result}";
			case "Pragma":
				return $"Pragma.Add(new NameValueHeaderValue(\"{location.Value}\"))";
			case ":protocol":
				return $"Protocol = \"{location.Value}\"";
			case "Proxy-Authorization":
				var proxyArguments = location.Value.Split([' '], StringSplitOptions.RemoveEmptyEntries)
					.Select(s => $"\"{s}\"")
					.Take(2);

				return $"ProxyAuthorization = new AuthenticationHeaderValue({String.Join(", ", proxyArguments)})";
			case "Referer":
				return $"Referrer = new Uri(\"{location.Value}\", UriKind.RelativeOrAbsolute)";
			case "TE":
				return $"TE.Add(new TransferCodingWithQualityHeaderValue(\"{location.Value}\"))";
			case "Trailer":
				return $"Trailer.Add(\"{location.Value}\")";
			case "Transfer-Encoding":
				return $"TransferEncoding.Add(new TransferCodingHeaderValue(\"{location.Value}\"))";
			case "TransferEncodingChunked" when Boolean.TryParse(location.Value, out var result):
				return $"TransferEncodingChunked = {result.ToString().ToLower()}";
			case "Upgrade":
				var upgradeArguments = location.Value.Split([' '], StringSplitOptions.RemoveEmptyEntries)
					.Select(s => $"\"{s}\"")
					.Take(2);
				return $"Upgrade.Add(new ProductHeaderValue({String.Join(", ", upgradeArguments)}))";
			case "User-Agent":
				var userAgentArguments = location.Value.Split([' '], StringSplitOptions.RemoveEmptyEntries)
					.Select(s => $"\"{s}\"")
					.Take(2);

				return $"UserAgent.Add(new ProductInfoHeaderValue({String.Join(", ", userAgentArguments)}))";
			case "Via":
				var viaArguments = location.Value.Split([' '], StringSplitOptions.RemoveEmptyEntries)
					.Select(s => $"\"{s}\"")
					.Take(4);

				return $"Via.Add(new ViaHeaderValue({String.Join(", ", viaArguments)}))";
			default:
				return defaultBuilder(location.Name, location.Value);
		}
	}

	public static void WriteDefaultRequestHeaders(ClassModel source, SourceWriter builder)
	{
		builder.Indentation = 2;

		using (builder.AppendIndentation("DefaultRequestHeaders = "))
		{
			foreach (var header in source.Attributes.Where(w => w.Location == HttpLocation.Header))
			{
				var result = ParseHeader(header, (header, value) => $"{{ \"{header}\", \"{value}\" }}");

				builder.WriteLine($"{result},");
			}
		}

		builder.Indentation = 1;
	}

	private static bool HasHoles(string value)
	{
		return HoleRegex.IsMatch(value);
	}

	private static string FillHoles(string value, params string[] fieldName)
	{
		return HoleRegex.Replace(value, m =>
		{
			var result = m.Value;

			for (var i = 0; i < fieldName.Length; i++)
			{
				result = m.Value.Replace(i.ToString(), fieldName[i]);
			}

			if (result == m.Value)
			{
				result = result
					.Replace("{", "{{")
					.Replace("}", "}}");
			}

			return result;
		});
	}
}