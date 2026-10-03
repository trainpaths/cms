using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using api_backend.Models.Auth;
using api_backend.Models.Dto;
using api_backend.Services.Auth.JWT;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace api_backend.Tests.Integration;

public class MediaTests : IClassFixture<ApiFactory>
{
	private const string Base = "/api/media";
	private readonly ApiFactory _factory;
	private readonly HttpClient _client;
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	// Smallest valid headers per format; the API only sniffs magic bytes.
	private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52];
	private static readonly byte[] Webp = [.. "RIFF"u8, 0x24, 0, 0, 0, .. "WEBPVP8 "u8];

	public MediaTests(ApiFactory factory)
	{
		_factory = factory;
		_client = factory.CreateCookielessClient();
	}

	[Fact]
	public async Task List_Anonymous_ReturnsUnauthorized()
	{
		var response = await _client.GetAsync(Base, Ct);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Upload_AsCustomer_ReturnsForbidden()
	{
		var token = CreateToken(Guid.NewGuid(), "customer", []);
		var response = await UploadAsync(token, Png, "a.png");
		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Upload_Png_StoresBlobAndServesItPublicly()
	{
		var token = await CreateStaffTokenAsync();

		var response = await UploadAsync(token, Png, "../../photo.png", alt: "  A photo ");
		response.StatusCode.Should().Be(HttpStatusCode.Created);
		var item = (await response.Content.ReadFromJsonAsync<MediaItem>(Ct))!;
		item.ContentType.Should().Be("image/png");
		item.FileName.Should().Be("photo.png");
		item.Alt.Should().Be("A photo");
		item.Size.Should().Be(Png.Length);
		item.Url.Should().MatchRegex("^/api/public/media/[0-9a-f]{32}\\.png$");
		_factory.BlobStorage.Blobs.Should().ContainKey(item.Url.Split('/')[^1]);

		var file = await _client.GetAsync(item.Url, Ct);
		file.StatusCode.Should().Be(HttpStatusCode.OK);
		file.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
		file.Headers.CacheControl!.ToString().Should().Contain("immutable");
		file.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("sandbox");
		(await file.Content.ReadAsByteArrayAsync(Ct)).Should().Equal(Png);
	}

	[Fact]
	public async Task Upload_TypeFromMagicBytes_NotClientHeader()
	{
		var token = await CreateStaffTokenAsync();
		var response = await UploadAsync(token, Webp, "x.png", contentType: "image/png");
		var item = (await response.Content.ReadFromJsonAsync<MediaItem>(Ct))!;
		item.ContentType.Should().Be("image/webp");
		item.Url.Should().EndWith(".webp");
	}

	[Theory]
	[InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>", "x.svg")]
	[InlineData("<html><script>alert(1)</script></html>", "x.png")]
	[InlineData("", "empty.png")]
	public async Task Upload_NonImage_ReturnsBadRequest(string content, string fileName)
	{
		var token = await CreateStaffTokenAsync();
		var response = await UploadAsync(token, Encoding.UTF8.GetBytes(content), fileName);
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Upload_OverLimit_IsRejected()
	{
		var token = await CreateStaffTokenAsync();
		var big = new byte[11 * 1024 * 1024];
		Png.CopyTo(big, 0);
		var response = await UploadAsync(token, big, "big.png");
		((int)response.StatusCode).Should().BeOneOf(400, 413);
	}

	[Fact]
	public async Task UpdateAlt_ThenList_ReturnsNewAlt()
	{
		var token = await CreateStaffTokenAsync();
		var item = await UploadItemAsync(token);

		var update = await SendAsync(HttpMethod.Put, $"{Base}/{item.Id}", token, new JsonObject { ["alt"] = "Updated" });
		update.StatusCode.Should().Be(HttpStatusCode.OK);

		var list = await (await SendAsync(HttpMethod.Get, Base, token)).Content.ReadFromJsonAsync<List<MediaItem>>(Ct);
		list!.Single(m => m.Id == item.Id).Alt.Should().Be("Updated");
	}

	[Fact]
	public async Task Rename_ChangesFileNameOnly()
	{
		var token = await CreateStaffTokenAsync();
		var item = await UploadItemAsync(token, alt: "Kept");

		var update = await SendAsync(HttpMethod.Put, $"{Base}/{item.Id}", token,
			new JsonObject { ["alt"] = "Kept", ["fileName"] = "  Team photo.png " });
		update.StatusCode.Should().Be(HttpStatusCode.OK);
		var renamed = (await update.Content.ReadFromJsonAsync<MediaItem>(Ct))!;
		renamed.FileName.Should().Be("Team photo.png");
		renamed.Url.Should().Be(item.Url);
		renamed.Alt.Should().Be("Kept");

		// omitted name = unchanged
		var altOnly = await SendAsync(HttpMethod.Put, $"{Base}/{item.Id}", token, new JsonObject { ["alt"] = "New" });
		(await altOnly.Content.ReadFromJsonAsync<MediaItem>(Ct))!.FileName.Should().Be("Team photo.png");
	}

	[Theory]
	[InlineData("   ")]
	[InlineData(null)]
	public async Task Rename_BlankOrTooLong_ReturnsBadRequest(string? name)
	{
		var token = await CreateStaffTokenAsync();
		var item = await UploadItemAsync(token);
		var update = await SendAsync(HttpMethod.Put, $"{Base}/{item.Id}", token,
			new JsonObject { ["fileName"] = name ?? new string('a', 256) });
		update.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Alt_Over100Chars_ReturnsBadRequest()
	{
		var token = await CreateStaffTokenAsync();
		(await UploadAsync(token, Png, "a.png", alt: new string('a', 101))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

		var item = await UploadItemAsync(token);
		var update = await SendAsync(HttpMethod.Put, $"{Base}/{item.Id}", token, new JsonObject { ["alt"] = new string('a', 101) });
		update.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		(await SendAsync(HttpMethod.Put, $"{Base}/{item.Id}", token, new JsonObject { ["alt"] = new string('a', 100) }))
			.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task Delete_RemovesBlobAndPublicFile()
	{
		var token = await CreateStaffTokenAsync();
		var item = await UploadItemAsync(token);
		var key = item.Url.Split('/')[^1];

		(await SendAsync(HttpMethod.Delete, $"{Base}/{item.Id}", token)).StatusCode.Should().Be(HttpStatusCode.NoContent);

		_factory.BlobStorage.Blobs.Should().NotContainKey(key);
		(await _client.GetAsync(item.Url, Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
		(await SendAsync(HttpMethod.Delete, $"{Base}/{item.Id}", token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Theory]
	[InlineData("nope.png")]
	[InlineData("..%2Fsecret")]
	[InlineData("00000000000000000000000000000000.svg")]
	public async Task PublicGet_UnknownOrMalformedKey_ReturnsNotFound(string key)
	{
		var response = await _client.GetAsync($"/api/public/media/{key}", Ct);
		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task Pages_IncludeReferencedMedia()
	{
		var token = await CreateStaffTokenAsync();
		var item = await UploadItemAsync(token, alt: "Hero");
		var slug = $"media-{Guid.NewGuid():N}"[..20];

		var create = await SendAsync(HttpMethod.Post, "/api/pages", token, new JsonObject
		{
			["title"] = "Media page",
			["slug"] = slug,
			["blocks"] = JsonNode.Parse($$"""
				[{ "id": "c1", "name": "card", "attributes": { "mediaId": "{{item.Id}}" }, "innerBlocks": [
					{ "id": "i1", "name": "image", "attributes": { "mediaId": "{{item.Id}}" }, "innerBlocks": [] },
					{ "id": "i2", "name": "image", "attributes": { "mediaId": "{{Guid.NewGuid()}}" }, "innerBlocks": [] }
				] }]
				"""),
		});
		var page = (await create.Content.ReadFromJsonAsync<PageDetail>(Ct))!;
		page.Media.Should().ContainSingle().Which.Should().Be(new MediaRef(item.Id, item.Url, "Hero"));

		await SendAsync(HttpMethod.Post, $"/api/pages/{page.Id}/publish", token);
		await SendAsync(HttpMethod.Put, $"{Base}/{item.Id}", token, new JsonObject { ["alt"] = "Renamed" });

		var published = await _client.GetFromJsonAsync<PublicPage>($"/api/public/pages/{slug}", Ct);
		published!.Media.Should().ContainSingle().Which.Alt.Should().Be("Renamed");
	}

	private async Task<MediaItem> UploadItemAsync(string token, string? alt = null)
	{
		var response = await UploadAsync(token, Png, "img.png", alt);
		response.StatusCode.Should().Be(HttpStatusCode.Created);
		return (await response.Content.ReadFromJsonAsync<MediaItem>(Ct))!;
	}

	private async Task<HttpResponseMessage> UploadAsync(
		string token, byte[] bytes, string fileName, string? alt = null, string contentType = "application/octet-stream")
	{
		using var form = new MultipartFormDataContent();
		var file = new ByteArrayContent(bytes);
		file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
		form.Add(file, "file", fileName);
		if (alt is not null)
			form.Add(new StringContent(alt), "alt");

		using var request = new HttpRequestMessage(HttpMethod.Post, Base) { Content = form };
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		return await _client.SendAsync(request, Ct);
	}

	private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string token, JsonNode? body = null)
	{
		using var request = new HttpRequestMessage(method, url);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		if (body is not null)
			request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
		return await _client.SendAsync(request, Ct);
	}

	private string CreateToken(Guid id, string userType, string[] roles)
	{
		var tokens = _factory.Services.GetRequiredService<JwtTokenService>();
		return tokens.CreateAccessToken(id, $"{userType}-{id}@test.com", userType, roles).Token;
	}

	/// <summary>Media rows reference their uploader, so the staff row must exist.</summary>
	private async Task<string> CreateStaffTokenAsync()
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		var hasher = scope.ServiceProvider.GetRequiredService<PasswordHashService>();
		var staff = new Staff { Email = $"media-{Guid.NewGuid()}@test.com", PasswordHash = hasher.Hash("password123") };
		db.Staff.Add(staff);
		await db.SaveChangesAsync(Ct);
		return CreateToken(staff.Id, "staff", ["staff"]);
	}
}
