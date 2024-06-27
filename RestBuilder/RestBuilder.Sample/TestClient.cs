using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RestBuilder.Sample;

[RestClient("Client")]
[BaseAddress("https://api.example.com/")]
[AllowAnyStatusCode]
public partial class TestClient : IDisposable
{
	[Get("{username}/User?")]
	[Header("Authorization", "Bearer 123")]
	[AllowAnyStatusCode]
	public partial void GetUserAsync(
		[Path("username")] string password,
		[QueryMap] Dictionary<string, List<int>?> name,
		CancellationToken token);

	[RequestModifier]
	private static void LogRequest(HttpRequestMessage request, CancellationToken token)
	{
		Console.WriteLine(request.ToString());
  }
	
	[ResponseDeserializer]
	private async static ValueTask<T?> ParseAsync<T>(HttpResponseMessage response, CancellationToken token)
	{
		return await response.Content.ReadFromJsonAsync<T>(token);
	}

	//[RequestBodySerializer]
	//private HttpContent Serialize<T>(T body)
	//{
	//	if (typeof(T) == typeof(String))
	//	{
	//		return new StringContent((String)(object)body);
	//	}

	//	if (typeof(T) == typeof(Stream))
	//	{
	//		return new StreamContent((Stream)(object)body);
	//	}

	//	return JsonContent.Create(body);
	//}

	[HttpClientInitializer]
	public static HttpClient CreateClient()
	{
		return new HttpClient();
	}
}