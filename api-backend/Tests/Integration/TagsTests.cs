using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using api_backend.Models.Auth;
using api_backend.Models.Dto;
using api_backend.Services.Auth.JWT;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace api_backend.Tests.Integration;

public class TagsTests : IClassFixture<ApiFactory>
{
	private const string Base = "/api/pages";
	private readonly ApiFactory _factory;
	private readonly HttpClient _client;
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public TagsTests(ApiFactory factory)
	{
		_factory = factory;
		_client = factory.CreateCookielessClient();
	}

	[Fact]
	public async Task ListTags_Anonymous_ReturnsUnauthorized()
	{
		(await _client.GetAsync("/api/tags", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task SetTags_NormalizesDedupsAndShowsInListAndDetail()
	{
		var token = await CreateStaffTokenAsync();
		var id = await CreatePageAsync(token);
		var unique = Unique();

		var response = await PutTagsAsync(token, id, [$"  Summer{unique} ", $"summer{unique}", "News"]);
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var detail = await response.Content.ReadFromJsonAsync<PageDetail>(Ct);
		detail!.Tags.Should().Equal("news", $"summer{unique}");

		var list = await (await SendAsync(HttpMethod.Get, Base, token)).Content.ReadFromJsonAsync<List<PageSummary>>(Ct);
		list!.Single(p => p.Id == id).Tags.Should().Equal("news", $"summer{unique}");
	}

	[Fact]
	public async Task SetTags_DoesNotBumpUpdatedAt()
	{
		var token = await CreateStaffTokenAsync();
		var id = await CreatePageAsync(token);
		var before = await (await SendAsync(HttpMethod.Get, $"{Base}/{id}", token)).Content.ReadFromJsonAsync<PageDetail>(Ct);

		var after = await (await PutTagsAsync(token, id, ["x"])).Content.ReadFromJsonAsync<PageDetail>(Ct);
		after!.UpdatedAt.Should().Be(before!.UpdatedAt);
	}

	[Fact]
	public async Task SetTags_InvalidInput_ReturnsBadRequest()
	{
		var token = await CreateStaffTokenAsync();
		var id = await CreatePageAsync(token);

		(await PutTagsAsync(token, id, ["   "])).StatusCode.Should().Be(HttpStatusCode.BadRequest);
		(await PutTagsAsync(token, id, ["two words"])).StatusCode.Should().Be(HttpStatusCode.BadRequest);
		(await PutTagsAsync(token, id, [new string('a', 33)])).StatusCode.Should().Be(HttpStatusCode.BadRequest);
		(await PutTagsAsync(token, id, Enumerable.Range(0, 21).Select(i => $"t{i}").ToArray()))
			.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		(await PutTagsAsync(token, Guid.NewGuid(), ["x"])).StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task Usage_CountsPagesAndDropsUnusedTags()
	{
		var token = await CreateStaffTokenAsync();
		var a = await CreatePageAsync(token);
		var b = await CreatePageAsync(token);
		var shared = $"shared-{Unique()}";
		var single = $"single-{Unique()}";

		await PutTagsAsync(token, a, [shared, single]);
		await PutTagsAsync(token, b, [shared]);

		var usage = await GetUsageAsync(token);
		usage.Single(t => t.Name == shared).PageCount.Should().Be(2);
		usage.Single(t => t.Name == single).PageCount.Should().Be(1);
		usage.IndexOf(usage.Single(t => t.Name == shared)).Should().BeLessThan(usage.IndexOf(usage.Single(t => t.Name == single)));

		// removed from its only page → tag row deleted
		await PutTagsAsync(token, a, [shared]);
		(await GetUsageAsync(token)).Should().NotContain(t => t.Name == single);
		(await TagExistsAsync(single)).Should().BeFalse();

		// deleting the pages deletes the now unused tag
		(await SendAsync(HttpMethod.Delete, $"{Base}/{a}", token)).StatusCode.Should().Be(HttpStatusCode.NoContent);
		(await SendAsync(HttpMethod.Delete, $"{Base}/{b}", token)).StatusCode.Should().Be(HttpStatusCode.NoContent);
		(await TagExistsAsync(shared)).Should().BeFalse();
	}

	private static string Unique() => Guid.NewGuid().ToString("N")[..8];

	private async Task<bool> TagExistsAsync(string name)
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		return await db.Tags.AnyAsync(t => t.Name == name, Ct);
	}

	private async Task<List<TagUsage>> GetUsageAsync(string token) =>
		(await (await SendAsync(HttpMethod.Get, "/api/tags", token)).Content.ReadFromJsonAsync<List<TagUsage>>(Ct))!;

	private Task<HttpResponseMessage> PutTagsAsync(string token, Guid id, string[] tags) =>
		SendAsync(HttpMethod.Put, $"{Base}/{id}/tags", token,
			new JsonObject { ["tags"] = new JsonArray(tags.Select(t => (JsonNode)t).ToArray()) });

	private async Task<Guid> CreatePageAsync(string token)
	{
		var create = await SendAsync(HttpMethod.Post, Base, token, new JsonObject { ["title"] = $"Tagged {Unique()}" });
		create.StatusCode.Should().Be(HttpStatusCode.Created);
		return (await create.Content.ReadFromJsonAsync<PageDetail>(Ct))!.Id;
	}

	private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string token, JsonNode? body = null)
	{
		using var request = new HttpRequestMessage(method, url);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		if (body is not null)
			request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
		return await _client.SendAsync(request, Ct);
	}

	private async Task<string> CreateStaffTokenAsync()
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		var hasher = scope.ServiceProvider.GetRequiredService<PasswordHashService>();
		var staff = new Staff { Email = $"tagger-{Guid.NewGuid()}@test.com", PasswordHash = hasher.Hash("password123") };
		db.Staff.Add(staff);
		await db.SaveChangesAsync(Ct);
		var tokens = _factory.Services.GetRequiredService<JwtTokenService>();
		return tokens.CreateAccessToken(staff.Id, staff.Email, "staff", ["staff"]).Token;
	}
}
