using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using api_backend.Models.Auth;
using api_backend.Models.Backup;
using api_backend.Models.Dto;
using api_backend.Services.Auth.JWT;
using api_backend.Services.Backup;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace api_backend.Tests.Integration;

/// <summary>Backup API without the Postgres client tools (the real round trip: <see cref="BackupRoundTripTests"/>).</summary>
public class BackupsTests : IClassFixture<ApiFactory>
{
	private const string Base = "/api/backups";
	private readonly ApiFactory _factory;
	private readonly HttpClient _client;
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public BackupsTests(ApiFactory factory)
	{
		_factory = factory;
		_client = factory.CreateCookielessClient();
	}

	[Fact]
	public async Task Endpoints_RequireSuperAdmin()
	{
		(await _client.GetAsync($"{Base}/settings", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

		var staff = await BackupTestHelpers.CreateStaffTokenAsync(_factory, superAdmin: false);
		(await Send(HttpMethod.Get, $"{Base}/settings", staff)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
		(await Send(HttpMethod.Post, Base, staff)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

		var admin = await BackupTestHelpers.CreateStaffTokenAsync(_factory, superAdmin: true);
		(await Send(HttpMethod.Get, $"{Base}/settings", admin)).StatusCode.Should().Be(HttpStatusCode.OK);
		(await Send(HttpMethod.Get, Base, admin)).StatusCode.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task Settings_DefaultsRoundTripAndValidation()
	{
		var admin = await BackupTestHelpers.CreateStaffTokenAsync(_factory, superAdmin: true);

		var defaults = (await (await Send(HttpMethod.Get, $"{Base}/settings", admin)).Content.ReadFromJsonAsync<BackupSettingsResponse>(Ct))!;
		defaults.Directory.Should().Be(_factory.BackupDirectory);
		defaults.MaxUploadBytes.Should().Be(2048L * 1024 * 1024);

		var put = await Send(HttpMethod.Put, $"{Base}/settings", admin, new JsonObject
		{
			["interval"] = "weekly",
			["timeOfDay"] = "04:30",
			["weekday"] = 2,
			["retention"] = 3,
		});
		put.StatusCode.Should().Be(HttpStatusCode.OK);
		var saved = (await (await Send(HttpMethod.Get, $"{Base}/settings", admin)).Content.ReadFromJsonAsync<BackupSettingsResponse>(Ct))!;
		saved.Interval.Should().Be(BackupInterval.Weekly);
		saved.TimeOfDay.Should().Be("04:30");
		saved.Weekday.Should().Be(2);
		saved.Retention.Should().Be(3);
		saved.NextRunAt.Should().NotBeNull();
		saved.NextRunAt!.Value.DayOfWeek.Should().Be(DayOfWeek.Tuesday);

		foreach (var (time, weekday, retention) in new[] { ("25:00", 0, 7), ("4:30", 0, 7), ("04:30", 7, 7), ("04:30", 0, 0) })
		{
			(await Send(HttpMethod.Put, $"{Base}/settings", admin, new JsonObject
			{
				["interval"] = "daily",
				["timeOfDay"] = time,
				["weekday"] = weekday,
				["retention"] = retention,
			})).StatusCode.Should().Be(HttpStatusCode.BadRequest, $"{time} / {weekday} / {retention}");
		}
	}

	[Theory]
	[InlineData("missing.tar.gz")]
	[InlineData("manual-20260101-000000.tar.gz")]
	[InlineData("..%2F..%2Fetc%2Fpasswd")]
	[InlineData(".work")]
	public async Task UnknownOrMalformedNames_AreNotFound(string name)
	{
		var admin = await BackupTestHelpers.CreateStaffTokenAsync(_factory, superAdmin: true);
		(await Send(HttpMethod.Get, $"{Base}/{name}", admin)).StatusCode.Should().Be(HttpStatusCode.NotFound);
		(await Send(HttpMethod.Delete, $"{Base}/{name}", admin)).StatusCode.Should().Be(HttpStatusCode.NotFound);
		(await Send(HttpMethod.Post, $"{Base}/{name}/restore", admin)).StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task WhileBusy_ReturnsConflict()
	{
		var admin = await BackupTestHelpers.CreateStaffTokenAsync(_factory, superAdmin: true);
		var backupLock = _factory.Services.GetRequiredService<BackupLock>();
		using (backupLock.TryEnter())
		{
			(await Send(HttpMethod.Post, Base, admin)).StatusCode.Should().Be(HttpStatusCode.Conflict);
		}
	}

	[Fact]
	public async Task DuringRestore_ApiAnswers503()
	{
		var admin = await BackupTestHelpers.CreateStaffTokenAsync(_factory, superAdmin: true);
		var backupLock = _factory.Services.GetRequiredService<BackupLock>();
		using (backupLock.Maintenance())
		{
			var response = await Send(HttpMethod.Get, "/api/pages", admin);
			response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
			response.Headers.RetryAfter.Should().NotBeNull();
			(await _client.GetAsync("/health", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
		}
		(await Send(HttpMethod.Get, "/api/pages", admin)).StatusCode.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task Upload_RejectsFilesThatAreNotBackups()
	{
		var admin = await BackupTestHelpers.CreateStaffTokenAsync(_factory, superAdmin: true);

		var junk = await BackupTestHelpers.UploadAsync(_client, admin, "junk.tar.gz"u8.ToArray(), Ct);
		junk.StatusCode.Should().Be(HttpStatusCode.BadRequest);

		var noDump = await BackupTestHelpers.UploadAsync(_client, admin,
			BackupTestHelpers.Archive(BackupTestHelpers.Manifest("20260101000000_Init"), dump: null), Ct);
		noDump.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		(await noDump.Content.ReadAsStringAsync(Ct)).Should().Contain("no database dump");
	}

	[Fact]
	public async Task Restore_RefusesBackupsOfANewerSchema_BeforeTouchingAnything()
	{
		var admin = await BackupTestHelpers.CreateStaffTokenAsync(_factory, superAdmin: true);
		var archive = BackupTestHelpers.Archive(BackupTestHelpers.Manifest("99990101000000_FromTheFuture"), dump: "not a real dump"u8.ToArray());

		var upload = await BackupTestHelpers.UploadAsync(_client, admin, archive, Ct);
		upload.StatusCode.Should().Be(HttpStatusCode.Created);
		var info = (await upload.Content.ReadFromJsonAsync<BackupInfo>(Ct))!;
		info.Kind.Should().Be(BackupKind.Upload);
		info.Name.Should().StartWith("upload-");
		info.MediaCount.Should().Be(0);

		var restore = await Send(HttpMethod.Post, $"{Base}/{info.Name}/restore", admin);
		restore.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		(await restore.Content.ReadAsStringAsync(Ct)).Should().Contain("newer CMS version");

		// no pre-restore backup was taken
		var list = (await (await Send(HttpMethod.Get, Base, admin)).Content.ReadFromJsonAsync<List<BackupInfo>>(Ct))!;
		list.Should().NotContain(b => b.Kind == BackupKind.PreRestore);

		(await Send(HttpMethod.Delete, $"{Base}/{info.Name}", admin)).StatusCode.Should().Be(HttpStatusCode.NoContent);
		(await Send(HttpMethod.Get, $"{Base}/{info.Name}", admin)).StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	private Task<HttpResponseMessage> Send(HttpMethod method, string url, string token, JsonNode? body = null) =>
		BackupTestHelpers.SendAsync(_client, method, url, token, body, Ct);
}

internal static class BackupTestHelpers
{
	public static bool PgToolsAvailable { get; } = DetectPgTools();

	public static async Task<HttpResponseMessage> SendAsync(
		HttpClient client, HttpMethod method, string url, string token, JsonNode? body, CancellationToken ct)
	{
		using var request = new HttpRequestMessage(method, url);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		if (body is not null)
			request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
		return await client.SendAsync(request, ct);
	}

	public static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string token, byte[] bytes, CancellationToken ct)
	{
		using var form = new MultipartFormDataContent();
		var file = new ByteArrayContent(bytes);
		file.Headers.ContentType = new MediaTypeHeaderValue("application/gzip");
		form.Add(file, "file", "backup.tar.gz");
		using var request = new HttpRequestMessage(HttpMethod.Post, "/api/backups/upload") { Content = form };
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		return await client.SendAsync(request, ct);
	}

	/// <summary>Staff rows are referenced by pages and media, so the account must exist.</summary>
	public static async Task<string> CreateStaffTokenAsync(ApiFactory factory, bool superAdmin)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		var hasher = scope.ServiceProvider.GetRequiredService<PasswordHashService>();
		var staff = new Staff { Email = $"backup-{Guid.NewGuid()}@test.com", PasswordHash = hasher.Hash("password123") };
		db.Staff.Add(staff);
		await db.SaveChangesAsync();
		var tokens = factory.Services.GetRequiredService<JwtTokenService>();
		string[] roles = superAdmin ? ["super_admin"] : ["staff"];
		return tokens.CreateAccessToken(staff.Id, staff.Email, "staff", roles).Token;
	}

	public static BackupManifest Manifest(string lastMigration) =>
		new(BackupArchive.Format, BackupArchive.Version, DateTimeOffset.UtcNow, BackupKind.Manual, "test", lastMigration, 0);

	public static byte[] Archive(BackupManifest manifest, byte[]? dump)
	{
		using var ms = new MemoryStream();
		using (var gzip = new GZipStream(ms, CompressionLevel.Fastest, leaveOpen: true))
		using (var tar = new TarWriter(gzip, TarEntryFormat.Pax))
		{
			Write(tar, BackupArchive.ManifestEntry, JsonSerializer.SerializeToUtf8Bytes(manifest, BackupArchive.Json));
			if (dump is not null)
				Write(tar, BackupArchive.DumpEntry, dump);
		}
		return ms.ToArray();

		static void Write(TarWriter tar, string name, byte[] data) =>
			tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, name) { DataStream = new MemoryStream(data) });
	}

	private static bool DetectPgTools()
	{
		try
		{
			using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("pg_dump", "--version")
			{
				RedirectStandardOutput = true,
				UseShellExecute = false,
			});
			process!.WaitForExit();
			return process.ExitCode == 0;
		}
		catch (System.ComponentModel.Win32Exception)
		{
			return false;
		}
	}
}
