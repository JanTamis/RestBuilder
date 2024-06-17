using System;
using System.Collections.Generic;
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
}