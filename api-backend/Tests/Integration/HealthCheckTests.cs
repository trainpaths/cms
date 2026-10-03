using System.Net;
using AwesomeAssertions;

namespace api_backend.Tests.Integration;

public class HealthCheckTests : IClassFixture<ApiFactory>
{
	private readonly HttpClient _client;

	public HealthCheckTests(ApiFactory factory)
	{
		_client = factory.CreateClient();
	}

	[Fact]
	public async Task Health_ReturnsOk_WhenDatabaseIsReachable()
	{
		var response = await _client.GetAsync("/health", TestContext.Current.CancellationToken);

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		content.Should().Be("Healthy");
	}
}
