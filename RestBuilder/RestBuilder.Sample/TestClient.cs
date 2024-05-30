using System.Threading;
using System.Threading.Tasks;

namespace RestBuilder.Sample;

[BaseAddress("https://api.example.com/")]
[Header("Authorization", "Bearer 123")]
public partial class TestClient
{
	[Header("testing")]
	public string TestProperty { get; set; }
	
	[Get("{username}/User")]
	[AllowAnyStatusCode]
	public partial Task<string> GetUserAsync(
		[Header("userId")] int id,
		[Header("username")] string name, 
		CancellationToken token);
}