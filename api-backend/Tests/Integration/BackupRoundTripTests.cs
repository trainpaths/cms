using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using api_backend.Models.Auth.JWT;
using api_backend.Models.Backup;
using api_backend.Models.Dto;
using api_backend.Models.Media;
using api_backend.Services.Backup;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace api_backend.Tests.Integration;

/// <summary>
/// Real backups and restores (pg_dump / pg_restore / psql against the test container). Skipped without
/// postgresql-client-18 on the PATH; CI installs it. Own class = own database: a restore replaces everything.
/// </summary>
public class BackupRoundTripTests : IClassFixture<ApiFactory>
{
	private const string Base = "/api/backups";
	private readonly ApiFactory _factory;
	private readonly HttpClient _client;
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public BackupRoundTripTests(ApiFactory factory)
	{
		_factory = factory;
		_client = factory.CreateCookielessClient();
	}

	[Fact]
	public async Task BackupThenRestore_BringsBackDataAndMedia_KeepsSchedule_EndsSessions()
	{
		Assert.SkipUnless(BackupTestHelpers.PgToolsAvailable, "pg_dump not installed (postgresql-client-18)");
		var admin = await BackupTestHelpers.CreateStaffTokenAsync(_factory, superAdmin: true);

		// state A: a page with an image
		var image = await AddMediaAsync("a");
		var kept = await CreatePageAsync(admin, "Kept page", image.Id);
		await AddRefreshTokenAsync();

		var create = await Send(HttpMethod.Post, Base, admin);
		create.StatusCode.Should().Be(HttpStatusCode.Created);
		var backup = (await create.Content.ReadFromJsonAsync<BackupInfo>(Ct))!;
		backup.Kind.Should().Be(BackupKind.Manual);
		backup.MediaCount.Should().Be(1);

		// state B: page deleted, image row + blob gone, another page + image, a new schedule
		(await PagesApi(HttpMethod.Delete, $"/{kept}", admin)).StatusCode.Should().Be(HttpStatusCode.NoContent);
		await DeleteMediaAsync(image);
		var later = await AddMediaAsync("b");
		await CreatePageAsync(admin, "Later page", later.Id);
		await Send(HttpMethod.Put, $"{Base}/settings", admin, new JsonObject
		{
			["interval"] = "daily",
			["timeOfDay"] = "02:15",
			["weekday"] = 0,
			["dayOfMonth"] = 1,
			["retention"] = 4,
		});

		var restore = await Send(HttpMethod.Post, $"{Base}/{backup.Name}/restore", admin);
		restore.StatusCode.Should().Be(HttpStatusCode.OK, await restore.Content.ReadAsStringAsync(Ct));
		var safety = (await restore.Content.ReadFromJsonAsync<RestoreResult>(Ct))!.PreRestoreBackup;
		safety.Should().StartWith("pre-restore-");

		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
			(await db.Pages.Select(p => p.Title).ToListAsync(Ct)).Should().Contain("Kept page").And.NotContain("Later page");
			(await db.MediaAssets.Select(m => m.StorageKey).ToListAsync(Ct)).Should().Equal(image.StorageKey);
			(await db.RefreshTokens.CountAsync(Ct)).Should().Be(0, "sessions are not part of a backup");
			var schedule = await db.BackupSettings.SingleAsync(Ct);
			schedule.Interval.Should().Be(BackupInterval.Daily);
			schedule.TimeOfDay.Should().Be(new TimeOnly(2, 15));
			schedule.Retention.Should().Be(4);
		}
		_factory.BlobStorage.Blobs.Should().ContainKey(image.StorageKey).And.NotContainKey(later.StorageKey);

		// undo with the pre-restore backup: state B again
		var admin2 = await BackupTestHelpers.CreateStaffTokenAsync(_factory, superAdmin: true);
		(await Send(HttpMethod.Post, $"{Base}/{safety}/restore", admin2)).StatusCode.Should().Be(HttpStatusCode.OK);
		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
			(await db.Pages.Select(p => p.Title).ToListAsync(Ct)).Should().Contain("Later page").And.NotContain("Kept page");
		}
		_factory.BlobStorage.Blobs.Should().ContainKey(later.StorageKey).And.NotContainKey(image.StorageKey);
	}

	[Fact]
	public async Task Download_ThenUpload_StoresAnIdenticalArchive()
	{
		Assert.SkipUnless(BackupTestHelpers.PgToolsAvailable, "pg_dump not installed (postgresql-client-18)");
		var admin = await BackupTestHelpers.CreateStaffTokenAsync(_factory, superAdmin: true);
		var backup = (await (await Send(HttpMethod.Post, Base, admin)).Content.ReadFromJsonAsync<BackupInfo>(Ct))!;

		var download = await Send(HttpMethod.Get, $"{Base}/{backup.Name}", admin);
		download.StatusCode.Should().Be(HttpStatusCode.OK);
		download.Content.Headers.ContentType!.MediaType.Should().Be("application/gzip");
		var bytes = await download.Content.ReadAsByteArrayAsync(Ct);
		bytes.Length.Should().Be((int)backup.Size);

		var upload = await BackupTestHelpers.UploadAsync(_client, admin, bytes, Ct);
		upload.StatusCode.Should().Be(HttpStatusCode.Created);
		var uploaded = (await upload.Content.ReadFromJsonAsync<BackupInfo>(Ct))!;
		uploaded.Kind.Should().Be(BackupKind.Upload);
		uploaded.CreatedAt.Should().Be(backup.CreatedAt);
		uploaded.Size.Should().Be(backup.Size);
	}

	[Fact]
	public async Task ScheduledRun_CreatesAutoBackups_AndPrunesToRetention()
	{
		Assert.SkipUnless(BackupTestHelpers.PgToolsAvailable, "pg_dump not installed (postgresql-client-18)");
		var now = DateTimeOffset.UtcNow;
		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
			await db.BackupSettings.ExecuteDeleteAsync(Ct);
			db.BackupSettings.Add(new BackupSettings
			{
				Interval = BackupInterval.Daily,
				TimeOfDay = TimeOnly.FromDateTime(now.UtcDateTime.AddMinutes(-1)),
				Retention = 1,
				UpdatedAt = now.AddDays(-2),
			});
			await db.SaveChangesAsync(Ct);
		}

		await RunScheduledAsync(now);
		await RunScheduledAsync(now.AddSeconds(5)); // not due again
		// pretend a day passed: due once more, then pruned to one
		await Task.Delay(1100, Ct); // distinct file name (seconds)
		await RunScheduledAsync(now.AddDays(1));

		var autos = Directory.GetFiles(_factory.BackupDirectory, "auto-*");
		autos.Should().ContainSingle();
		using (var scope = _factory.Services.CreateScope())
		{
			var settings = await scope.ServiceProvider.GetRequiredService<AppDbContext>().BackupSettings.SingleAsync(Ct);
			settings.LastRunAt.Should().BeCloseTo(now.AddDays(1), TimeSpan.FromMilliseconds(1)); // postgres: µs
			settings.LastError.Should().BeNull();
		}
	}

	private async Task RunScheduledAsync(DateTimeOffset now)
	{
		using var scope = _factory.Services.CreateScope();
		await scope.ServiceProvider.GetRequiredService<BackupService>().RunScheduledAsync(now, Ct);
	}

	private async Task<MediaAsset> AddMediaAsync(string content)
	{
		var asset = new MediaAsset { StorageKey = $"{Guid.NewGuid():N}.png", FileName = $"{content}.png", ContentType = "image/png" };
		_factory.BlobStorage.Blobs[asset.StorageKey] = (System.Text.Encoding.UTF8.GetBytes(content), "image/png");
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		db.MediaAssets.Add(asset);
		await db.SaveChangesAsync(Ct);
		return asset;
	}

	private async Task DeleteMediaAsync(MediaAsset asset)
	{
		_factory.BlobStorage.Blobs.TryRemove(asset.StorageKey, out _);
		using var scope = _factory.Services.CreateScope();
		await scope.ServiceProvider.GetRequiredService<AppDbContext>().MediaAssets
			.Where(m => m.Id == asset.Id).ExecuteDeleteAsync(Ct);
	}

	private async Task AddRefreshTokenAsync()
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		var staffId = await db.Staff.Select(s => s.Id).FirstAsync(Ct);
		db.RefreshTokens.Add(new RefreshToken
		{
			TokenHash = Guid.NewGuid().ToString("N"),
			UserType = UserType.Staff,
			StaffId = staffId,
			ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
		});
		await db.SaveChangesAsync(Ct);
	}

	private async Task<Guid> CreatePageAsync(string token, string title, Guid mediaId)
	{
		var response = await PagesApi(HttpMethod.Post, "", token, new JsonObject
		{
			["title"] = title,
			["blocks"] = JsonNode.Parse($$"""[{ "id": "i1", "name": "image", "attributes": { "mediaId": "{{mediaId}}" }, "innerBlocks": [] }]"""),
		});
		response.StatusCode.Should().Be(HttpStatusCode.Created);
		return (await response.Content.ReadFromJsonAsync<PageDetail>(Ct))!.Id;
	}

	private Task<HttpResponseMessage> PagesApi(HttpMethod method, string path, string token, JsonNode? body = null) =>
		BackupTestHelpers.SendAsync(_client, method, "/api/pages" + path, token, body, Ct);

	private Task<HttpResponseMessage> Send(HttpMethod method, string url, string token, JsonNode? body = null) =>
		BackupTestHelpers.SendAsync(_client, method, url, token, body, Ct);
}
