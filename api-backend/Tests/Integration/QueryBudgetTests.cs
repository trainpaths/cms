using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using api_backend.Migrations;
using api_backend.Models.Auth;
using api_backend.Models.Dto;
using api_backend.Models.Pages;
using api_backend.Services.Auth.JWT;
using api_backend.Services.Pages;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace api_backend.Tests.Integration;

/// <summary>
/// DB round trips of the hot paths (<see cref="QueryCounter"/>), the public menu/site config cache and the stored
/// block count.
/// </summary>
public class QueryBudgetTests : IClassFixture<ApiFactory>
{
	private readonly ApiFactory _factory;
	private readonly HttpClient _client;
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public QueryBudgetTests(ApiFactory factory)
	{
		_factory = factory;
		_client = factory.CreateCookielessClient();
	}

	private QueryCounter Counter => _factory.Services.GetRequiredService<QueryCounter>();

	[Fact]
	public async Task PublicHtml_IsOneQuery()
	{
		var token = await CreateStaffTokenAsync();
		var (_, slug) = await CreatePublishedAsync(token, "Budget html");

		var count = await CountAsync(HttpMethod.Get, $"/api/public/html/{slug}", null);
		count.Should().Be(1, Counter.Log);
		// a join on the PK, not a window function over every stored page's HTML
		Counter.Log.Should().NotContain("ROW_NUMBER");
		(await CountAsync(HttpMethod.Get, "/api/public/html/no-such-page", null)).Should().Be(1, Counter.Log);
	}

	[Fact]
	public async Task PageList_IsOneQuery()
	{
		var token = await CreateStaffTokenAsync();
		var (id, _) = await CreatePublishedAsync(token, "Budget list");
		(await SendAsync(HttpMethod.Put, $"/api/pages/{id}", token, new JsonObject { ["tags"] = new JsonArray("a", "b") }))
			.StatusCode.Should().Be(HttpStatusCode.OK);

		(await CountAsync(HttpMethod.Get, "/api/pages", token)).Should().Be(1, Counter.Log);
	}

	[Fact]
	public async Task PageDetail_FoldsTagsIntoThePageQuery()
	{
		var token = await CreateStaffTokenAsync();
		var (id, _) = await CreatePublishedAsync(token, "Budget detail");
		(await SendAsync(HttpMethod.Put, $"/api/pages/{id}", token, new JsonObject { ["tags"] = new JsonArray("zeta", "alpha") }))
			.StatusCode.Should().Be(HttpStatusCode.OK);

		// page + tags, media refs (none here: no query)
		(await CountAsync(HttpMethod.Get, $"/api/pages/{id}", token)).Should().Be(1, Counter.Log);
		var detail = await (await SendAsync(HttpMethod.Get, $"/api/pages/{id}", token)).Content.ReadFromJsonAsync<PageDetail>(Ct);
		detail!.Tags.Should().Equal("alpha", "zeta");
	}

	[Fact]
	public async Task Autosave_IsThreeQueries()
	{
		var token = await CreateStaffTokenAsync();
		var (id, _) = await CreatePublishedAsync(token, "Budget autosave");

		// page, its tags, the update
		(await CountAsync(HttpMethod.Put, $"/api/pages/{id}", token, new JsonObject { ["blocks"] = Nested() }))
			.Should().Be(3, Counter.Log);
	}

	[Fact]
	public async Task WarmPublicState_IsAtMostTwoQueries()
	{
		var token = await CreateStaffTokenAsync();
		var (_, slug) = await CreatePublishedAsync(token, "Budget state");
		await GetAsync<PublicStateResponse>($"/api/public/state/{slug}");

		// page (+ media refs when it has images); menu + config cached
		(await CountAsync(HttpMethod.Get, $"/api/public/state/{slug}", null)).Should().BeLessThanOrEqualTo(2, Counter.Log);
		(await CountAsync(HttpMethod.Get, "/api/public/state", null)).Should().Be(0, Counter.Log);
	}

	[Fact]
	public async Task PublicMenuAndSiteConfig_AreCached()
	{
		await GetAsync<PublicMenu>("/api/public/menus/main");
		await GetAsync<SiteConfigResponse>("/api/public/site-config");

		(await CountAsync(HttpMethod.Get, "/api/public/menus/main", null)).Should().Be(0, Counter.Log);
		(await CountAsync(HttpMethod.Get, "/api/public/menus/MAIN", null)).Should().Be(0, Counter.Log);
		(await CountAsync(HttpMethod.Get, "/api/public/site-config", null)).Should().Be(0, Counter.Log);
	}

	[Fact]
	public async Task MainMenuSave_ShowsUpRightAway()
	{
		var token = await CreateStaffTokenAsync();
		var menus = await (await SendAsync(HttpMethod.Get, "/api/menus", token)).Content.ReadFromJsonAsync<List<MenuDetail>>(Ct);
		var main = menus!.Single(m => m.Handle == "main");
		await GetAsync<PublicMenu>("/api/public/menus/main");

		var label = $"Cached {Guid.NewGuid():N}"[..20];
		await SetMenuItemsAsync(token, main.Id, label);
		(await GetAsync<PublicMenu>("/api/public/menus/main")).Items.Should().ContainSingle(i => i.Label == label);
	}

	[Fact]
	public async Task OtherMenuSave_ShowsUpRightAway()
	{
		var token = await CreateStaffTokenAsync();
		var handle = $"footer-{Guid.NewGuid():N}"[..20];
		var create = await SendAsync(HttpMethod.Post, "/api/menus", token, new JsonObject { ["handle"] = handle });
		create.StatusCode.Should().Be(HttpStatusCode.Created);
		var menu = await create.Content.ReadFromJsonAsync<MenuDetail>(Ct);
		(await GetAsync<PublicMenu>($"/api/public/menus/{handle}")).Items.Should().BeEmpty();

		await SetMenuItemsAsync(token, menu!.Id, "Footer link");
		(await GetAsync<PublicMenu>($"/api/public/menus/{handle}")).Items.Should().ContainSingle(i => i.Label == "Footer link");

		(await SendAsync(HttpMethod.Delete, $"/api/menus/{menu.Id}", token)).StatusCode.Should().Be(HttpStatusCode.NoContent);
		(await _client.GetAsync($"/api/public/menus/{handle}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task PublishedPageRename_ShowsUpInMenuRightAway()
	{
		var token = await CreateStaffTokenAsync();
		var handle = $"nav-{Guid.NewGuid():N}"[..16];
		var menu = await (await SendAsync(HttpMethod.Post, "/api/menus", token, new JsonObject { ["handle"] = handle }))
			.Content.ReadFromJsonAsync<MenuDetail>(Ct);
		var (id, _) = await CreatePublishedAsync(token, "Before rename");
		// unlabeled page link: shows the page title
		var items = new JsonArray(new JsonObject
		{
			["id"] = "p1", ["label"] = "", ["pageId"] = id, ["children"] = new JsonArray(),
		});
		(await SendAsync(HttpMethod.Put, $"/api/menus/{menu!.Id}", token, new JsonObject { ["items"] = items }))
			.StatusCode.Should().Be(HttpStatusCode.OK);
		(await GetAsync<PublicMenu>($"/api/public/menus/{handle}")).Items.Single().Label.Should().Be("Before rename");

		(await SendAsync(HttpMethod.Put, $"/api/pages/{id}", token, new JsonObject { ["title"] = "After rename" }))
			.StatusCode.Should().Be(HttpStatusCode.OK);
		(await GetAsync<PublicMenu>($"/api/public/menus/{handle}")).Items.Single().Label.Should().Be("After rename");
	}

	[Fact]
	public async Task SiteConfigSave_ShowsUpRightAway()
	{
		var token = await CreateStaffTokenAsync();
		await GetAsync<SiteConfigResponse>("/api/public/site-config");

		var name = $"Firm {Guid.NewGuid():N}"[..16];
		var put = await SendAsync(HttpMethod.Put, "/api/site-config", token,
			new JsonObject { ["fields"] = new JsonObject { ["firmName"] = name }, ["groups"] = new JsonObject() });
		put.StatusCode.Should().Be(HttpStatusCode.OK);
		var saved = await put.Content.ReadFromJsonAsync<SiteConfigResponse>(Ct);

		var cached = await GetAsync<SiteConfigResponse>("/api/public/site-config");
		cached.Fields["firmName"].Should().Be(name);
		// the PUT response is built without a re-read: same as what a read returns
		cached.UpdatedAt.Should().Be(saved!.UpdatedAt);
		cached.Groups.Keys.Should().BeEquivalentTo(saved.Groups.Keys);
	}

	[Fact]
	public async Task BlockCount_FollowsUpdates()
	{
		var token = await CreateStaffTokenAsync();
		var (id, slug) = await CreatePublishedAsync(token, "Counted");
		(await SendAsync(HttpMethod.Put, $"/api/pages/{id}", token, new JsonObject { ["blocks"] = Nested() }))
			.StatusCode.Should().Be(HttpStatusCode.OK);

		var list = await (await SendAsync(HttpMethod.Get, "/api/pages", token)).Content.ReadFromJsonAsync<List<PageSummary>>(Ct);
		list!.Single(p => p.Slug == slug).BlockCount.Should().Be(3);
	}

	[Fact]
	public async Task BackfillSql_MatchesCountBlocks()
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		List<List<Block>> trees =
		[
			[],
			[new Block { Id = "a", Name = "paragraph" }],
			JsonSerializer.Deserialize<List<Block>>(Nested(), JsonSerializerOptions.Web)!,
		];
		var pages = trees.Select((blocks, i) => new Page { Title = $"Backfill {i}", Slug = $"backfill-{i}-{Guid.NewGuid():N}"[..20], Blocks = blocks })
			.ToList();
		db.Pages.AddRange(pages);
		await db.SaveChangesAsync(Ct);
		// a stored null innerBlocks counts as empty
		var odd = new Page { Title = "Backfill odd", Slug = $"backfill-odd-{Guid.NewGuid():N}"[..20] };
		db.Pages.Add(odd);
		await db.SaveChangesAsync(Ct);
		await db.Database.ExecuteSqlAsync(
			$$"""UPDATE pages SET "Blocks" = '[{"id":"x","name":"p","attributes":{},"innerBlocks":null}]'::jsonb WHERE "Id" = {{odd.Id}}""", Ct);

		await db.Database.ExecuteSqlRawAsync(AddPageBlockCount.BackfillSql, Ct);

		var ids = pages.Select(p => p.Id).Append(odd.Id).ToList();
		var stored = await db.Pages.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, Ct);
		foreach (var page in pages)
			stored[page.Id].BlockCount.Should().Be(PageService.CountBlocks(page.Blocks));
		stored[odd.Id].BlockCount.Should().Be(1);
	}

	// 3 blocks: a group with two children
	private static JsonArray Nested() => JsonNode.Parse("""
		[{ "id": "g", "name": "group", "attributes": {}, "innerBlocks": [
			{ "id": "c1", "name": "paragraph", "attributes": {}, "innerBlocks": [] },
			{ "id": "c2", "name": "paragraph", "attributes": {}, "innerBlocks": [] }] }]
		""")!.AsArray();

	private async Task<int> CountAsync(HttpMethod method, string url, string? token, JsonNode? body = null)
	{
		Counter.Reset();
		using var request = new HttpRequestMessage(method, url);
		request.Headers.Add(QueryCounter.Header, "1");
		if (body is not null)
			request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
		if (token is not null)
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		var response = await _client.SendAsync(request, Ct);
		((int)response.StatusCode).Should().BeLessThan(500, await response.Content.ReadAsStringAsync(Ct));
		return Counter.Count;
	}

	private async Task<T> GetAsync<T>(string url)
	{
		var response = await _client.GetAsync(url, Ct);
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
	}

	private async Task SetMenuItemsAsync(string token, Guid menuId, string label)
	{
		var items = new JsonArray(new JsonObject
		{
			["id"] = "m1", ["label"] = label, ["url"] = "/somewhere", ["children"] = new JsonArray(),
		});
		(await SendAsync(HttpMethod.Put, $"/api/menus/{menuId}", token, new JsonObject { ["items"] = items }))
			.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	private async Task<(Guid Id, string Slug)> CreatePublishedAsync(string token, string title)
	{
		var create = await SendAsync(HttpMethod.Post, "/api/pages", token, new JsonObject
		{
			["title"] = title,
			["slug"] = $"budget-{Guid.NewGuid():N}"[..20],
			["blocks"] = new JsonArray(new JsonObject
			{
				["id"] = "b1", ["name"] = "paragraph", ["attributes"] = new JsonObject(), ["innerBlocks"] = new JsonArray(),
			}),
		});
		create.StatusCode.Should().Be(HttpStatusCode.Created);
		var page = await create.Content.ReadFromJsonAsync<PageDetail>(Ct);
		(await SendAsync(HttpMethod.Put, $"/api/pages/{page!.Id}", token, new JsonObject { ["status"] = "published" })).StatusCode.Should().Be(HttpStatusCode.OK);
		return (page.Id, page.Slug);
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
		var staff = new Staff { Email = $"budget-{Guid.NewGuid()}@test.com", PasswordHash = hasher.Hash("password123") };
		db.Staff.Add(staff);
		await db.SaveChangesAsync(Ct);
		return _factory.Services.GetRequiredService<JwtTokenService>()
			.CreateAccessToken(staff.Id, staff.Email, "staff", ["staff"]).Token;
	}
}
