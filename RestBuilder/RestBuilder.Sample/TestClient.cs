using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace RestBuilder.Sample;

[BaseAddress("https://api.example.com/")]
[Header("Authorization", "Bearer 123")]
public partial class TestClient
{
	[Get("{username}/User")]
	[AllowAnyStatusCode]
	public partial Task<string> GetUserAsync(
		[Path("username")] string name,
		[Query("password", UrlEncode = false)] string password,
		CancellationToken token);
}