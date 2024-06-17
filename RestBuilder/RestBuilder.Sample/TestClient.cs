using System;
using System.Collections.Generic;
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
	public partial Task<bool> GetUserAsync(
		[Path("username")] string password, [Body] bool body,
		[QueryMap] Dictionary<string, List<int>?> name,
		CancellationToken token);

	[RequestModifier]
	private static void LogRequest(HttpRequestMessage request)
	{
		Console.WriteLine(request.ToString());
  }

	[ResponseDeserializer]
	private async static ValueTask<T?> ParseAsync<T>(HttpResponseMessage response, CancellationToken token)
	{
		return await response.Content.ReadFromJsonAsync<T>(token);
	}
	
	[RequestBodySerializer]
	private ValueTask<HttpContent> Serialize<T>(T body)
	{
		return new ValueTask<HttpContent>(JsonContent.Create(body));
	}
}