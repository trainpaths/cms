using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using api_backend.Models.Auth;
using api_backend.Models.Dto;
using api_backend.Models.Media;
using api_backend.Models.Pages;
using api_backend.Services.Auth.JWT;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace api_backend.Tests.Integration;

public class PagesTests : IClassFixture<ApiFactory>
{
	private const string Base = "/api/pages";
	private readonly ApiFactory _factory;
	private readonly HttpClient _client;
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public PagesTests(ApiFactory factory)
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
	public async Task List_AsCustomer_ReturnsForbidden()
	{
		var token = CreateToken(Guid.NewGuid(), "customer", []);
		var response = await SendAsync(HttpMethod.Get, Base, token);
		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Crud_AsStaff_RoundTripsNestedBlocks()
	{
		var token = await CreateStaffTokenAsync();
		var slug = $"crud-{Guid.NewGuid():N}"[..20];
		var blocks = JsonNode.Parse("""
			[{ "id": "c1", "name": "card",
			   "attributes": { "title": "Hello", "blockWidth": "wide", "level": 2, "open": true },
			   "innerBlocks": [{ "id": "l1", "name": "link", "attributes": { "label": "Docs", "url": "https://x.test" }, "innerBlocks": [] }] }]
			""");

		var create = await SendAsync(HttpMethod.Post, Base, token, new JsonObject
		{
			["title"] = "Crud page",
			["slug"] = slug,
			["blocks"] = blocks,
		});
		create.StatusCode.Should().Be(HttpStatusCode.Created);
		var created = await create.Content.ReadFromJsonAsync<JsonObject>(Ct);
		var id = created!["id"]!.GetValue<string>();

		var fetched = await (await SendAsync(HttpMethod.Get, $"{Base}/{id}", token)).Content.ReadFromJsonAsync<JsonObject>(Ct);
		fetched!["status"]!.GetValue<string>().Should().Be("draft");
		JsonNode.DeepEquals(fetched["blocks"], blocks).Should().BeTrue();

		var update = await SendAsync(HttpMethod.Put, $"{Base}/{id}", token, new JsonObject { ["title"] = "Renamed" });
		update.StatusCode.Should().Be(HttpStatusCode.OK);
		// slim response: no blocks echoed back
		var updated = await update.Content.ReadFromJsonAsync<JsonObject>(Ct);
		updated!["title"]!.GetValue<string>().Should().Be("Renamed");
		updated.ContainsKey("blocks").Should().BeFalse();
		updated["blockCount"]!.GetValue<int>().Should().Be(2);

		var list = await (await SendAsync(HttpMethod.Get, Base, token)).Content.ReadFromJsonAsync<List<PageSummary>>(Ct);
		list!.Should().Contain(p => p.Slug == slug && p.BlockCount == 2);

		(await SendAsync(HttpMethod.Delete, $"{Base}/{id}", token)).StatusCode.Should().Be(HttpStatusCode.NoContent);
		(await SendAsync(HttpMethod.Get, $"{Base}/{id}", token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task PublicEndpoint_OnlyServesPublishedPages()
	{
		var token = await CreateStaffTokenAsync();
		var slug = $"pub-{Guid.NewGuid():N}"[..20];
		var create = await SendAsync(HttpMethod.Post, Base, token, new JsonObject { ["title"] = "Public", ["slug"] = slug });
		var id = (await create.Content.ReadFromJsonAsync<JsonObject>(Ct))!["id"]!.GetValue<string>();

		(await _client.GetAsync($"/api/public/pages/{slug}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

		(await SendAsync(HttpMethod.Put, $"{Base}/{id}", token, new JsonObject { ["status"] = "published" })).StatusCode.Should().Be(HttpStatusCode.OK);
		var publicResponse = await _client.GetAsync($"/api/public/pages/{slug}", Ct);
		publicResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var page = await publicResponse.Content.ReadFromJsonAsync<PublicPage>(Ct);
		page!.Title.Should().Be("Public");

		(await SendAsync(HttpMethod.Put, $"{Base}/{id}", token, new JsonObject { ["status"] = "draft" })).StatusCode.Should().Be(HttpStatusCode.OK);
		(await _client.GetAsync($"/api/public/pages/{slug}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task Update_ContentStatusAndTags_InOneCall()
	{
		var token = await CreateStaffTokenAsync();
		var slug = $"one-{Guid.NewGuid():N}"[..20];
		var create = await SendAsync(HttpMethod.Post, Base, token, new JsonObject { ["title"] = "Draft", ["slug"] = slug });
		var id = (await create.Content.ReadFromJsonAsync<PageDetail>(Ct))!.Id;

		var response = await SendAsync(HttpMethod.Put, $"{Base}/{id}", token, new JsonObject
		{
			["title"] = "Live",
			["status"] = "published",
			["tags"] = new JsonArray("News", "alpha"),
		});
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var summary = (await response.Content.ReadFromJsonAsync<PageSummary>(Ct))!;
		summary.Title.Should().Be("Live");
		summary.Status.Should().Be(PageStatus.Published);
		summary.PublishedAt.Should().NotBeNull();
		summary.Tags.Should().Equal("alpha", "news");
		(await _client.GetFromJsonAsync<PublicPage>($"/api/public/pages/{slug}", Ct))!.Title.Should().Be("Live");
	}

	[Fact]
	public async Task Update_InvalidField_ChangesNothing()
	{
		var token = await CreateStaffTokenAsync();
		var create = await SendAsync(HttpMethod.Post, Base, token, new JsonObject { ["title"] = "Atomic" });
		var id = (await create.Content.ReadFromJsonAsync<PageDetail>(Ct))!.Id;

		var response = await SendAsync(HttpMethod.Put, $"{Base}/{id}", token, new JsonObject
		{
			["title"] = "Changed",
			["status"] = "published",
			["tags"] = new JsonArray("two words"),
		});
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		var detail = await GetDetailAsync(token, id);
		detail.Title.Should().Be("Atomic");
		detail.Status.Should().Be(PageStatus.Draft);
		detail.Tags.Should().BeEmpty();
	}

	[Fact]
	public async Task Duplicate_CreatesDraftCopy_WithBlocksMetaAndTags()
	{
		var token = await CreateStaffTokenAsync();
		var slug = $"orig-{Guid.NewGuid():N}"[..20];
		var blocks = JsonNode.Parse("""
			[{ "id": "g", "name": "group", "attributes": {}, "innerBlocks": [
				{ "id": "p", "name": "paragraph", "attributes": { "text": "hi" }, "innerBlocks": [] }] }]
			""");
		var create = await SendAsync(HttpMethod.Post, Base, token,
			new JsonObject { ["title"] = "Original", ["slug"] = slug, ["blocks"] = blocks });
		var id = (await create.Content.ReadFromJsonAsync<PageDetail>(Ct))!.Id;
		(await SendAsync(HttpMethod.Put, $"{Base}/{id}", token, new JsonObject
		{
			["metaTitle"] = "Meta", ["status"] = "published", ["tags"] = new JsonArray("copied"),
		})).EnsureSuccessStatusCode();

		var response = await SendAsync(HttpMethod.Post, $"{Base}/{id}/duplicate", token);
		response.StatusCode.Should().Be(HttpStatusCode.Created);
		var copy = (await response.Content.ReadFromJsonAsync<PageDetail>(Ct))!;
		copy.Id.Should().NotBe(id);
		copy.Title.Should().Be("Original (copy)");
		copy.Slug.Should().Be($"{slug}-2");
		copy.Status.Should().Be(PageStatus.Draft);
		copy.MetaTitle.Should().Be("Meta");
		copy.Tags.Should().Equal("copied");
		var copyJson = await (await SendAsync(HttpMethod.Get, $"{Base}/{copy.Id}", token)).Content.ReadFromJsonAsync<JsonObject>(Ct);
		JsonNode.DeepEquals(copyJson!["blocks"], blocks).Should().BeTrue();

		var list = await (await SendAsync(HttpMethod.Get, Base, token)).Content.ReadFromJsonAsync<List<PageSummary>>(Ct);
		list!.Single(p => p.Id == copy.Id).BlockCount.Should().Be(2);
		(await SendAsync(HttpMethod.Post, $"{Base}/{Guid.NewGuid()}/duplicate", token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task PublicState_PageMenuAndConfig_InOneCall()
	{
		var token = await CreateStaffTokenAsync();
		var slug = $"state-{Guid.NewGuid():N}"[..20];
		var create = await SendAsync(HttpMethod.Post, Base, token, new JsonObject { ["title"] = "Stateful", ["slug"] = slug });
		var id = (await create.Content.ReadFromJsonAsync<PageDetail>(Ct))!.Id;

		// draft, unknown slug, no slug: page null, menu + config present
		foreach (var url in new[] { $"/api/public/state/{slug}", "/api/public/state/no-such-page", "/api/public/state" })
		{
			var state = await _client.GetFromJsonAsync<PublicStateResponse>(url, Ct);
			state!.Page.Should().BeNull(url);
			state.Menu!.Handle.Should().Be("main");
			state.Config.Fields.Should().ContainKey("firmName");
		}

		(await SendAsync(HttpMethod.Put, $"{Base}/{id}", token, new JsonObject { ["status"] = "published" })).EnsureSuccessStatusCode();
		var published = await _client.GetFromJsonAsync<PublicStateResponse>($"/api/public/state/{slug.ToUpperInvariant()}", Ct);
		published!.Page!.Title.Should().Be("Stateful");
	}

	[Fact]
	public async Task Create_ReservedSlug_ReturnsBadRequest()
	{
		var token = await CreateStaffTokenAsync();
		var response = await SendAsync(HttpMethod.Post, Base, token, new JsonObject { ["title"] = "x", ["slug"] = "login" });
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Create_DuplicateSlug_ReturnsConflict()
	{
		var token = await CreateStaffTokenAsync();
		var slug = $"dup-{Guid.NewGuid():N}"[..20];
		await SendAsync(HttpMethod.Post, Base, token, new JsonObject { ["title"] = "a", ["slug"] = slug });
		var response = await SendAsync(HttpMethod.Post, Base, token, new JsonObject { ["title"] = "b", ["slug"] = slug });
		response.StatusCode.Should().Be(HttpStatusCode.Conflict);
	}

	[Fact]
	public async Task Update_BodyAboveGlobalLimit_IsAccepted()
	{
		var token = await CreateStaffTokenAsync();
		var create = await SendAsync(HttpMethod.Post, Base, token, new JsonObject { ["title"] = "Big" });
		var id = (await create.Content.ReadFromJsonAsync<JsonObject>(Ct))!["id"]!.GetValue<string>();

		var blocks = new JsonArray();
		for (var i = 0; i < 200; i++)
		{
			blocks.Add(new JsonObject
			{
				["id"] = $"p{i}",
				["name"] = "paragraph",
				["attributes"] = new JsonObject { ["text"] = new string('x', 500) },
				["innerBlocks"] = new JsonArray(),
			});
		}

		// ~100 KB: over the 64 KB global cap, under the 2 MB page cap.
		var response = await SendAsync(HttpMethod.Put, $"{Base}/{id}", token, new JsonObject { ["blocks"] = blocks });
		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task Meta_IsPartialTrimmedAndPublic_EmptyClears_OverlongRejected()
	{
		var token = await CreateStaffTokenAsync();
		var slug = $"meta-{Guid.NewGuid():N}"[..20];
		var create = await SendAsync(HttpMethod.Post, Base, token, new JsonObject { ["title"] = "Meta page", ["slug"] = slug });
		var created = (await create.Content.ReadFromJsonAsync<PageDetail>(Ct))!;
		created.MetaTitle.Should().BeEmpty();
		var id = created.Id;

		(await SendAsync(HttpMethod.Put, $"{Base}/{id}", token,
			new JsonObject { ["metaTitle"] = " About us ", ["metaDescription"] = "Who we are." })).EnsureSuccessStatusCode();
		var detail = await GetDetailAsync(token, id);
		detail.MetaTitle.Should().Be("About us");
		detail.MetaDescription.Should().Be("Who we are.");

		// partial: a title-only save keeps the meta
		(await SendAsync(HttpMethod.Put, $"{Base}/{id}", token, new JsonObject { ["title"] = "Renamed" })).EnsureSuccessStatusCode();
		detail = await GetDetailAsync(token, id);
		detail.MetaTitle.Should().Be("About us");

		await SendAsync(HttpMethod.Put, $"{Base}/{id}", token, new JsonObject { ["status"] = "published" });
		var pub = await _client.GetFromJsonAsync<PublicPage>($"/api/public/pages/{slug}", Ct);
		pub!.MetaTitle.Should().Be("About us");
		pub.MetaDescription.Should().Be("Who we are.");

		(await SendAsync(HttpMethod.Put, $"{Base}/{id}", token, new JsonObject { ["metaTitle"] = "" })).EnsureSuccessStatusCode();
		detail = await GetDetailAsync(token, id);
		detail.MetaTitle.Should().BeEmpty();

		(await SendAsync(HttpMethod.Put, $"{Base}/{id}", token, new JsonObject { ["metaTitle"] = new string('t', 71) }))
			.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		(await SendAsync(HttpMethod.Put, $"{Base}/{id}", token, new JsonObject { ["metaDescription"] = new string('d', 201) }))
			.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Import_CreatesDraftWithMetaAndTags_DeduplicatesSlug_ReportsMissingMedia()
	{
		var token = await CreateStaffTokenAsync();
		var slug = $"imp-{Guid.NewGuid():N}"[..20];
		await SendAsync(HttpMethod.Post, Base, token, new JsonObject { ["title"] = "Existing", ["slug"] = slug });

		var known = new MediaAsset { StorageKey = $"{Guid.NewGuid():N}.png", FileName = "a.png", ContentType = "image/png" };
		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
			db.MediaAssets.Add(known);
			await db.SaveChangesAsync(Ct);
		}
		var missing = Guid.NewGuid();
		var blocks = JsonNode.Parse($$"""
			[{ "id": "i1", "name": "image", "attributes": { "mediaId": "{{known.Id}}" }, "innerBlocks": [] },
			 { "id": "g1", "name": "group", "attributes": {},
			   "innerBlocks": [{ "id": "i2", "name": "image", "attributes": { "mediaId": "{{missing}}" }, "innerBlocks": [] }] }]
			""");

		var response = await SendAsync(HttpMethod.Post, $"{Base}/import", token, new JsonObject
		{
			["title"] = " Imported ",
			["slug"] = slug,
			["blocks"] = blocks,
			["metaTitle"] = "Meta",
			["metaDescription"] = "Desc",
			["tags"] = new JsonArray("Docs", "docs", "news"),
		});

		response.StatusCode.Should().Be(HttpStatusCode.Created);
		var result = (await response.Content.ReadFromJsonAsync<ImportPageResult>(Ct))!;
		result.Page.Title.Should().Be("Imported");
		result.Page.Slug.Should().Be($"{slug}-2");
		result.Page.Status.Should().Be(Models.Pages.PageStatus.Draft);
		result.Page.MetaTitle.Should().Be("Meta");
		result.Page.MetaDescription.Should().Be("Desc");
		result.Page.Tags.Should().Equal("docs", "news");
		result.Page.Media.Should().ContainSingle().Which.Id.Should().Be(known.Id);
		result.MissingMediaIds.Should().Equal(missing);
	}

	[Fact]
	public async Task Import_ReservedOrMissingSlug_FallsBackToValidSlug()
	{
		var token = await CreateStaffTokenAsync();

		var reserved = await SendAsync(HttpMethod.Post, $"{Base}/import", token,
			new JsonObject { ["title"] = "x", ["slug"] = "login", ["blocks"] = new JsonArray() });
		(await reserved.Content.ReadFromJsonAsync<ImportPageResult>(Ct))!.Page.Slug.Should().StartWith("login-page");

		var title = $"Fresh {Guid.NewGuid():N}"[..20];
		var noSlug = await SendAsync(HttpMethod.Post, $"{Base}/import", token,
			new JsonObject { ["title"] = title, ["blocks"] = new JsonArray() });
		(await noSlug.Content.ReadFromJsonAsync<ImportPageResult>(Ct))!.Page.Slug.Should().Be(title.ToLowerInvariant().Replace(' ', '-'));
	}

	[Fact]
	public async Task Import_InvalidBlocksOrTags_ReturnsBadRequest_AndCreatesNothing()
	{
		var token = await CreateStaffTokenAsync();
		var title = $"Bad {Guid.NewGuid():N}";
		var badBlocks = JsonNode.Parse("""[{ "id": "", "name": "paragraph", "attributes": {}, "innerBlocks": [] }]""");

		(await SendAsync(HttpMethod.Post, $"{Base}/import", token, new JsonObject { ["title"] = title, ["blocks"] = badBlocks }))
			.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		(await SendAsync(HttpMethod.Post, $"{Base}/import", token,
				new JsonObject { ["title"] = title, ["blocks"] = new JsonArray(), ["tags"] = new JsonArray("two words") }))
			.StatusCode.Should().Be(HttpStatusCode.BadRequest);

		var list = await (await SendAsync(HttpMethod.Get, Base, token)).Content.ReadFromJsonAsync<List<PageSummary>>(Ct);
		list!.Should().NotContain(p => p.Title == title);
	}

	[Fact]
	public async Task Import_AsCustomer_ReturnsForbidden()
	{
		var token = CreateToken(Guid.NewGuid(), "customer", []);
		var response = await SendAsync(HttpMethod.Post, $"{Base}/import", token,
			new JsonObject { ["title"] = "x", ["blocks"] = new JsonArray() });
		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	private async Task<PageDetail> GetDetailAsync(string token, Guid id) =>
		(await (await SendAsync(HttpMethod.Get, $"{Base}/{id}", token)).Content.ReadFromJsonAsync<PageDetail>(Ct))!;

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

	/// <summary>Pages reference their author, so the staff row must exist.</summary>
	private async Task<string> CreateStaffTokenAsync()
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		var hasher = scope.ServiceProvider.GetRequiredService<PasswordHashService>();
		var staff = new Staff { Email = $"editor-{Guid.NewGuid()}@test.com", PasswordHash = hasher.Hash("password123") };
		db.Staff.Add(staff);
		await db.SaveChangesAsync(Ct);
		return CreateToken(staff.Id, "staff", ["staff"]);
	}
}
