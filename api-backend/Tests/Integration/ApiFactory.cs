using api_backend;
using api_backend.Services.Cms;
using api_backend.Services.Email;
using api_backend.Services.Media;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace api_backend.Tests.Integration;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
		.WithDatabase("testdb")
		.WithUsername("testuser")
		.WithPassword("testpass")
		.Build();

	/// <summary>Captures emails so tests can recover the verification / reset token.</summary>
	public CollectingEmailSender EmailSender { get; } = new();

	/// <summary>Satisfies the S3 startup validation; no store is contacted (see <see cref="BlobStorage"/>).</summary>
	public static readonly Dictionary<string, string?> StorageSettings = new()
	{
		["Storage:S3:ServiceUrl"] = "http://s3.invalid",
		["Storage:S3:AccessKey"] = "test",
		["Storage:S3:SecretKey"] = "test",
	};

	/// <summary>Instance config of the test app: defaults with public auth on (the customer auth tests need it).</summary>
	public static readonly CmsConfig Cms = CmsConfig.Default with { PublicAuth = true };

	/// <summary>Replaces SeaweedFS/S3; tests inspect stored blobs directly.</summary>
	public InMemoryBlobStorage BlobStorage { get; } = new();

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.ConfigureServices(services =>
		{
			var descriptor = services.SingleOrDefault(
				d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
			if (descriptor != null)
				services.Remove(descriptor);

			services.AddDbContext<AppDbContext>(options =>
				options.UseNpgsql(_postgres.GetConnectionString()));

			services.RemoveAll<IEmailSender>();
			services.AddSingleton<IEmailSender>(EmailSender);

			services.RemoveAll<IBlobStorage>();
			services.AddSingleton<IBlobStorage>(BlobStorage);

			services.RemoveAll<CmsConfig>();
			services.AddSingleton(Cms);
		});

		builder.ConfigureAppConfiguration((_, config) =>
		{
			config.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
				["RateLimiting:PermitLimit"] = "100000",
				["RateLimiting:WindowSeconds"] = "1",
				["RateLimiting:General:PermitLimit"] = "100000",
				["RateLimiting:General:WindowSeconds"] = "1",
				["RateLimiting:Pages:PermitLimit"] = "100000",
				["RateLimiting:Pages:WindowSeconds"] = "1",
				// tests count/create pages themselves; PageSeederTests turns it back on
				["Bootstrap:SeedPages"] = "false",
			});
			config.AddInMemoryCollection(StorageSettings);
		});

		builder.UseEnvironment("Development");
	}

	public async ValueTask InitializeAsync()
	{
		await _postgres.StartAsync();
	}

	public new async ValueTask DisposeAsync()
	{
		await _postgres.DisposeAsync();
		await base.DisposeAsync();
	}
}
