using System.Net;
using System.Net.Http.Json;
using api_backend.Models.Dto;
using api_backend.Services.Cms;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace api_backend.Tests.Integration;

public class RateLimitingTests : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
		.WithDatabase("ratelimit_testdb")
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
					services.AddSingleton<IStartupFilter, FakeClientIpStartupFilter>();
					// customer endpoints need public auth (instance config)
					services.RemoveAll<CmsConfig>();
					services.AddSingleton(ApiFactory.Cms);
				});

				builder.ConfigureAppConfiguration((_, config) =>
				{
					config.AddInMemoryCollection(new Dictionary<string, string?>
					{
						["RateLimiting:PermitLimit"] = "3",
						["RateLimiting:WindowSeconds"] = "60"
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
	public async Task Login_ExceedingRateLimit_Returns429WithRetryAfter()
	{
		var request = new LoginRequest("nonexistent@test.com", "password123");

		HttpResponseMessage? lastResponse = null;
		for (var i = 0; i < 5; i++)
		{
			lastResponse = await _client.PostAsJsonAsync("/api/auth/customer/login", request, TestContext.Current.CancellationToken);
			if (lastResponse.StatusCode == HttpStatusCode.TooManyRequests)
				break;
		}

		lastResponse.Should().NotBeNull();
		lastResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
		lastResponse.Headers.Should().ContainKey("Retry-After");
	}

	[Fact]
	public async Task Register_ExceedingRateLimit_Returns429()
	{
		HttpResponseMessage? lastResponse = null;
		for (var i = 0; i < 5; i++)
		{
			var request = new RegisterRequest(
				$"ratelimit-{Guid.NewGuid()}@test.com",
				"password123",
				"Test");
			lastResponse = await _client.PostAsJsonAsync("/api/auth/customer/register", request, TestContext.Current.CancellationToken);
			if (lastResponse.StatusCode == HttpStatusCode.TooManyRequests)
				break;
		}

		lastResponse.Should().NotBeNull();
		lastResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
	}

	[Fact]
	public async Task Login_LimitIsPerClientIp()
	{
		var request = new LoginRequest("nonexistent@test.com", "password123");

		for (var i = 0; i < 4; i++)
			await LoginFromAsync("10.0.0.1", request);

		(await LoginFromAsync("10.0.0.1", request)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
		(await LoginFromAsync("10.0.0.2", request)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	private async Task<HttpResponseMessage> LoginFromAsync(string ip, LoginRequest body)
	{
		using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/customer/login");
		request.Headers.Add(FakeClientIpStartupFilter.Header, ip);
		request.Content = JsonContent.Create(body);
		return await _client.SendAsync(request, TestContext.Current.CancellationToken);
	}

	// TestServer has no remote IP; take it from a test header instead.
	private sealed class FakeClientIpStartupFilter : IStartupFilter
	{
		public const string Header = "X-Test-Client-IP";

		public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
		{
			app.Use(async (context, nextMiddleware) =>
			{
				if (context.Request.Headers.TryGetValue(Header, out var ip))
					context.Connection.RemoteIpAddress = IPAddress.Parse(ip.ToString());
				await nextMiddleware(context);
			});
			next(app);
		};
	}
}
