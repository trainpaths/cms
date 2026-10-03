using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using api_backend.Models.Auth;
using api_backend.Models.Dto;
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
		var updated = await update.Content.ReadFromJsonAsync<JsonObject>(Ct);
		updated!["title"]!.GetValue<string>().Should().Be("Renamed");
		JsonNode.DeepEquals(updated["blocks"], blocks).Should().BeTrue();

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

		(await SendAsync(HttpMethod.Post, $"{Base}/{id}/publish", token)).StatusCode.Should().Be(HttpStatusCode.OK);
		var publicResponse = await _client.GetAsync($"/api/public/pages/{slug}", Ct);
		publicResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var page = await publicResponse.Content.ReadFromJsonAsync<PublicPage>(Ct);
		page!.Title.Should().Be("Public");

		(await SendAsync(HttpMethod.Post, $"{Base}/{id}/unpublish", token)).StatusCode.Should().Be(HttpStatusCode.OK);
		(await _client.GetAsync($"/api/public/pages/{slug}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
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

		var update = await SendAsync(HttpMethod.Put, $"{Base}/{id}", token,
			new JsonObject { ["metaTitle"] = " About us ", ["metaDescription"] = "Who we are." });
		var detail = (await update.Content.ReadFromJsonAsync<PageDetail>(Ct))!;
		detail.MetaTitle.Should().Be("About us");
		detail.MetaDescription.Should().Be("Who we are.");

		// partial: a title-only save keeps the meta
		detail = (await (await SendAsync(HttpMethod.Put, $"{Base}/{id}", token, new JsonObject { ["title"] = "Renamed" }))
			.Content.ReadFromJsonAsync<PageDetail>(Ct))!;
		detail.MetaTitle.Should().Be("About us");

		await SendAsync(HttpMethod.Post, $"{Base}/{id}/publish", token);
		var pub = await _client.GetFromJsonAsync<PublicPage>($"/api/public/pages/{slug}", Ct);
		pub!.MetaTitle.Should().Be("About us");
		pub.MetaDescription.Should().Be("Who we are.");

		detail = (await (await SendAsync(HttpMethod.Put, $"{Base}/{id}", token, new JsonObject { ["metaTitle"] = "" }))
			.Content.ReadFromJsonAsync<PageDetail>(Ct))!;
		detail.MetaTitle.Should().BeEmpty();

		(await SendAsync(HttpMethod.Put, $"{Base}/{id}", token, new JsonObject { ["metaTitle"] = new string('t', 71) }))
			.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		(await SendAsync(HttpMethod.Put, $"{Base}/{id}", token, new JsonObject { ["metaDescription"] = new string('d', 201) }))
			.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
