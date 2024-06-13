using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RestBuilder.Sample;

[RestClient("Client")]
// [BaseAddress("https://api.example.com/")]
[Header("Authorization", "Bearer 123")]
public partial class TestClient
{
	[Get("{username}/User?")]
	[AllowAnyStatusCode]
	public partial Task<string> GetUserAsync(
		[Path("username")] string password,
		[QueryMap] Dictionary<string, int> name,		
		CancellationToken token);
}