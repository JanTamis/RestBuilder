using System.Threading;
using System.Threading.Tasks;

namespace RestBuilder.Sample;

[BaseAddress("https://api.example.com/")]
public partial class TestClient
{
	[Get("User")]
	[AllowAnyStatusCode]
	public partial Task<string> GetUserAsync(
		[Header("userId")] int id,
		[Header("name")] string name, 
		CancellationToken token);
}