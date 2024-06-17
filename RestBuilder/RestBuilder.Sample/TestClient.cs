using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RestBuilder.Sample;

[RestClient("Client")]
[BaseAddress("https://api.example.com/")]
public partial class TestClient : IDisposable
{
	[Get("{username}/User?")]
	[Header("Authorization", "Bearer 123")]
	[AllowAnyStatusCode]
	public partial Task<string> GetUserAsync(
		[Path("username")] string password,
		[QueryMap] Dictionary<string, List<int>?> name,
		CancellationToken token);

	[RequestModifier]
	private static void LogRequest(HttpRequestMessage request)
	{
		Console.WriteLine(request.ToString());
  }
}