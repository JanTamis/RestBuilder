using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RestBuilder.Sample;

[RestClient("Client")]
[BaseAddress("https://api.example.com")]
[AllowAnyStatusCode]
public partial class TestClient : IDisposable
{
	[RequestQueryParamSerializer] 
	public IEnumerable<KeyValuePair<string, string>> SerializeParameter<T>(string key, T value)
	{
		yield return KeyValuePair.Create<string, string>(key, value.ToString());
	}
	
	[Get("{username}/User?")]
	[Header("Authorization", "Bearer 123")]
	[AllowAnyStatusCode]
	public partial ValueTask<string> GetUserAsync(
		[Path("username")] string password,
		[Body] string body,
		[Query] string? test,
		// [QueryMap] Dictionary<string, int> name,
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