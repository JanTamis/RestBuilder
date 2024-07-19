using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using RestBuilder.Core.Attributes;

namespace RestBuilder.Sample;

[RestClient("Client")]
[BaseAddress("https://api.example.com")]
[AllowAnyStatusCode]
public partial class TestClient : IDisposable
{
	[RequestQueryParamSerializer]
	public static IEnumerable<KeyValuePair<string, string>> SerializeParameter<T>(string key, T value)
	{
		yield return KeyValuePair.Create(key, value?.ToString() ?? String.Empty);
	}
	
	[Get("{username}/User")]
	[Header("Authorization", "Bearer 123")]
	public partial ValueTask<string> GetUserAsync(
		[Path("username")] string password,
		[Body] string body,
		[QueryMap(UrlEncode = false)] Dictionary<string, int> name,
		CancellationToken token);

	[RequestModifier]
	private static void LogRequest(HttpRequestMessage request)
	{
		Console.WriteLine(request.ToString());
	}

	[ResponseDeserializer]
	private static Task<string> ParseAsync(HttpResponseMessage response, CancellationToken token)
	{
		return response.Content.ReadAsStringAsync(token);
	}

	[RequestBodySerializer]
	private HttpContent SerializeString(string body)
	{
		return new StringContent(body);
	}

	// [HttpClientInitializer]
	// public static HttpClient CreateClient()
	// {
	// 	return new HttpClient();
	// }
}