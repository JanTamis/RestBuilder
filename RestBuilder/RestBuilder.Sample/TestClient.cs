using System.Threading;
using System.Threading.Tasks;

namespace RestBuilder.Sample;

[BaseAddress("https://api.example.com/")]
public partial class TestClient
{
	[Get("{username}/User")]
	[AllowAnyStatusCode]
	public partial Task<string> GetUserAsync(
		[Header("userId")] int id,
		[Query("username")] string name, 
		CancellationToken token);
}