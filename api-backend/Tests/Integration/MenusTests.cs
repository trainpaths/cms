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

public class MenusTests : IClassFixture<ApiFactory>
{
	private const string Base = "/api/menus";
	private readonly ApiFactory _factory;
	private readonly HttpClient _client;
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public MenusTests(ApiFactory factory)
	{
		_factory = factory;
		_client = factory.CreateCookielessClient();
	}

	[Fact]
	public async Task List_Anonymous_ReturnsUnauthorized()
	{
		(await _client.GetAsync(Base, Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task MainMenu_IsSeededWithHomeAndLoginLinks()
	{
		var menu = await _client.GetFromJsonAsync<PublicMenu>("/api/public/menus/main", Ct);
		menu!.Items.Select(i => (i.Label, i.Url)).Should().Equal(("Home", "/"), ("Login", "/login"));
	}

	[Fact]
	public async Task MainMenu_IsSeededFirstAndCannotBeDeleted()
	{
		var token = await CreateStaffTokenAsync();
		var menus = await (await SendAsync(HttpMethod.Get, Base, token)).Content.ReadFromJsonAsync<List<MenuSummary>>(Ct);
		var main = menus![0];
		main.Handle.Should().Be("main");

		(await SendAsync(HttpMethod.Delete, $"{Base}/{main.Id}", token)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Crud_NormalizesHandleAndRoundTripsItems()
	{
		var token = await CreateStaffTokenAsync();
		var (pageId, _) = await CreatePageAsync(token, publish: false);
		var unique = Guid.NewGuid().ToString("N")[..8];

		var created = await CreateMenuAsync(token, $"  Footer Links {unique} ");
		created.Handle.Should().Be($"footer-links-{unique}");

		var items = new JsonArray(
			Item("a", "About", pageId: pageId, children: new JsonArray(Item("b", " Insta ", url: " https://instagram.com/x "))),
			Item("c", "Group", children: new JsonArray(Item("d", "", pageId: pageId), Item("e", "Admin", url: "/admin/pages"))));
		var update = await SendAsync(HttpMethod.Put, $"{Base}/{created.Id}", token, Update(items));
		update.StatusCode.Should().Be(HttpStatusCode.OK);
		var detail = await update.Content.ReadFromJsonAsync<MenuDetail>(Ct);
		detail!.Handle.Should().Be(created.Handle);
		detail.Items[0].Children[0].Label.Should().Be("Insta");
		detail.Items[0].Children[0].Url.Should().Be("https://instagram.com/x");

		var list = await (await SendAsync(HttpMethod.Get, Base, token)).Content.ReadFromJsonAsync<List<MenuSummary>>(Ct);
		list!.Single(m => m.Id == created.Id).ItemCount.Should().Be(5);
		detail.Items[1].Children[1].Url.Should().Be("/admin/pages");

		(await SendAsync(HttpMethod.Delete, $"{Base}/{created.Id}", token)).StatusCode.Should().Be(HttpStatusCode.NoContent);
		(await SendAsync(HttpMethod.Get, $"{Base}/{created.Id}", token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task Update_InvalidItems_ReturnsBadRequest()
	{
		var token = await CreateStaffTokenAsync();
		var menu = await CreateMenuAsync(token, $"invalid-{Guid.NewGuid():N}"[..20]);
		var (pageId, _) = await CreatePageAsync(token, publish: false);

		async Task Expect400(JsonArray items) =>
			(await SendAsync(HttpMethod.Put, $"{Base}/{menu.Id}", token, Update(items)))
				.StatusCode.Should().Be(HttpStatusCode.BadRequest);

		await Expect400(new JsonArray(Item("a", "Bad", url: "javascript:alert(1)")));
		await Expect400(new JsonArray(Item("a", "Other site", url: "//evil.test/x")));
		await Expect400(new JsonArray(Item("a", "Relative", url: "about")));
		await Expect400(new JsonArray(Item("a", "Both", pageId: pageId, url: "https://x.test")));
		await Expect400(new JsonArray(Item("a", "")));
		await Expect400(new JsonArray(Item("a", "Gone", pageId: Guid.NewGuid())));
		await Expect400(new JsonArray(Item("a", "One"), Item("a", "Dup id")));

		var deep = Item("l11", "Too deep");
		for (var i = 10; i >= 1; i--)
			deep = Item($"l{i}", "Level", children: new JsonArray(deep));
		await Expect400(new JsonArray(deep));
	}

	[Fact]
	public async Task Create_TakenOrInvalidHandle_IsRejected()
	{
		var token = await CreateStaffTokenAsync();
		var a = await CreateMenuAsync(token, $"conflict-{Guid.NewGuid():N}"[..20]);

		async Task<HttpStatusCode> Create(string handle) =>
			(await SendAsync(HttpMethod.Post, Base, token, new JsonObject { ["handle"] = handle })).StatusCode;

		(await Create(a.Handle.ToUpperInvariant())).Should().Be(HttpStatusCode.Conflict);
		(await Create("main")).Should().Be(HttpStatusCode.Conflict);
		(await Create(" !!! ")).Should().Be(HttpStatusCode.BadRequest);
		(await Create(new string('a', 51))).Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Public_ResolvesPublishedPagesAndDropsOthers()
	{
		var token = await CreateStaffTokenAsync();
		var (published, slug) = await CreatePageAsync(token, publish: true);
		var (draft, _) = await CreatePageAsync(token, publish: false);
		var menu = await CreateMenuAsync(token, $"public-{Guid.NewGuid():N}"[..20]);

		var items = new JsonArray(
			Item("p", "", pageId: published),
			Item("d", "", pageId: draft, children: new JsonArray(Item("x", "External", url: "https://x.test"))),
			Item("dl", "Labeled draft", pageId: draft, children: new JsonArray(Item("y", "Child", pageId: published))),
			Item("f", "Empty folder", children: new JsonArray(Item("z", "", pageId: draft))));
		(await SendAsync(HttpMethod.Put, $"{Base}/{menu.Id}", token, Update(items)))
			.StatusCode.Should().Be(HttpStatusCode.OK);

		var response = await _client.GetAsync($"/api/public/menus/{menu.Handle}", Ct);
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var result = await response.Content.ReadFromJsonAsync<PublicMenu>(Ct);

		result!.Items.Should().HaveCount(3);
		result.Items[0].Slug.Should().Be(slug);
		result.Items[0].Label.Should().StartWith("Menu page");
		// unlabeled draft link: its children move up a level
		result.Items[1].Label.Should().Be("External");
		result.Items[1].Url.Should().Be("https://x.test");
		// labeled draft link with children: becomes a folder
		result.Items[2].Label.Should().Be("Labeled draft");
		result.Items[2].Slug.Should().BeNull();
		result.Items[2].Children.Single().Slug.Should().Be(slug);

		// slug changes are followed
		await SendAsync(HttpMethod.Put, $"/api/pages/{published}", token, new JsonObject { ["slug"] = slug + "-new" });
		var after = await _client.GetFromJsonAsync<PublicMenu>($"/api/public/menus/{menu.Handle}", Ct);
		after!.Items[0].Slug.Should().Be(slug + "-new");

		(await _client.GetAsync("/api/public/menus/does-not-exist", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	private static JsonObject Item(string id, string label, Guid? pageId = null, string? url = null, JsonArray? children = null) => new()
	{
		["id"] = id,
		["label"] = label,
		["pageId"] = pageId?.ToString(),
		["url"] = url,
		["children"] = children ?? new JsonArray(),
	};

	private static JsonObject Update(JsonArray items) => new() { ["items"] = items };

	private async Task<MenuDetail> CreateMenuAsync(string token, string handle)
	{
		var response = await SendAsync(HttpMethod.Post, Base, token, new JsonObject { ["handle"] = handle });
		response.StatusCode.Should().Be(HttpStatusCode.Created);
		return (await response.Content.ReadFromJsonAsync<MenuDetail>(Ct))!;
	}

	private async Task<(Guid Id, string Slug)> CreatePageAsync(string token, bool publish)
	{
		var create = await SendAsync(HttpMethod.Post, "/api/pages", token,
			new JsonObject { ["title"] = $"Menu page {Guid.NewGuid():N}"[..20] });
		var page = (await create.Content.ReadFromJsonAsync<PageDetail>(Ct))!;
		if (publish)
			(await SendAsync(HttpMethod.Post, $"/api/pages/{page.Id}/publish", token)).EnsureSuccessStatusCode();
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
		var staff = new Staff { Email = $"menus-{Guid.NewGuid()}@test.com", PasswordHash = hasher.Hash("password123") };
		db.Staff.Add(staff);
		await db.SaveChangesAsync(Ct);
		var tokens = _factory.Services.GetRequiredService<JwtTokenService>();
		return tokens.CreateAccessToken(staff.Id, staff.Email, "staff", ["staff"]).Token;
	}
}
