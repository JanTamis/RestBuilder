using System.Threading;
using System.Threading.Tasks;

namespace RestBuilder.Sample;

[BaseAddress("https://api.example.com/")]
public partial class TestClient
{
	[Get("User")]
	[AllowAnyStatusCode]
	public partial Task<string> GetUserAsync([Query] int id, CancellationToken token);
}