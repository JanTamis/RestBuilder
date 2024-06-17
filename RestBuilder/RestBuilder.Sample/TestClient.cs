using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace RestBuilder.Sample;

[RestClient("Client")]
[BaseAddress("https://api.example.com/")]
[Header("Authorization", "Bearer 123")]
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
	public static void AddAuthorizationAsync(HttpRequestMessage request)
	{
		// See if the request has an authorize header
		var auth = request.Headers.Authorization;

		if (auth != null)
		{
			request.Headers.Authorization = new AuthenticationHeaderValue(auth.Scheme, "123");
		}
	}
}