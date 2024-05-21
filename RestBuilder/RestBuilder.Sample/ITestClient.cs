using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RestBuilder.Sample;

[BaseAddress("https://api.example.com/")]
public interface ITestClient
{
	[Get("User")]
	[AllowAnyStatusCode]
	Task<string> GetUserAsync([Query(Format = "N2")] int id, CancellationToken token);
}