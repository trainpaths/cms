using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace api_backend.Tests.Integration;

public class ExceptionHandlerTests : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
		.WithDatabase("exception_testdb")
		.WithUsername("testuser")
		.WithPassword("testpass")
		.Build();

	private WebApplicationFactory<Program> _factory = null!;
	private HttpClient _client = null!;

	public async ValueTask InitializeAsync()
	{
		await _postgres.StartAsync();

		_factory = new WebApplicationFactory<Program>()
			.WithWebHostBuilder(builder =>
			{
				builder.ConfigureServices(services =>
				{
					var descriptor = services.SingleOrDefault(
						d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
					if (descriptor != null)
						services.Remove(descriptor);

					services.AddDbContext<AppDbContext>(options =>
						options.UseNpgsql(_postgres.GetConnectionString()));
				});

				builder.ConfigureAppConfiguration((_, config) =>
				{
					config.AddInMemoryCollection(new Dictionary<string, string?>
					{
						["RateLimiting:PermitLimit"] = "1000"
					});
					config.AddInMemoryCollection(ApiFactory.StorageSettings);
				});

				builder.UseEnvironment("Development");
			});

		_client = _factory.CreateClient();
	}

	public async ValueTask DisposeAsync()
	{
		_client.Dispose();
		await _factory.DisposeAsync();
		await _postgres.DisposeAsync();
	}

	[Fact]
	public async Task UnhandledException_ReturnsProblemDetails()
	{
		var response = await _client.GetAsync("/_diagnostics/throw", TestContext.Current.CancellationToken);

		response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
		response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

		var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
		json.TryGetProperty("status", out var status).Should().BeTrue();
		status.GetInt32().Should().Be(500);

		json.TryGetProperty("title", out var title).Should().BeTrue();
		title.GetString().Should().NotBeNullOrWhiteSpace();

		json.TryGetProperty("traceId", out var traceId).Should().BeTrue();
		traceId.GetString().Should().NotBeNullOrWhiteSpace();

		json.TryGetProperty("detail", out var detail).Should().BeTrue();
		detail.GetString().Should().Contain("Test exception");
	}
}
