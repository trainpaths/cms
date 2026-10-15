using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using api_backend.Controllers;
using api_backend.Models.Auth;
using api_backend.Models.Dto;
using api_backend.Services.Auth.JWT;
using api_backend.Services.Cms;
using api_backend.Services.Rendering;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace api_backend.Tests.Integration;

/// <summary>Stands in for the Node render service; output encodes the input so tests can assert on it.</summary>
public sealed class FakeRenderer : IRendererClient
{
	public volatile string Version = "v1";
	public ConcurrentQueue<string> Rendered { get; } = new();

	/// <summary>While set, every render waits for it: tests look at what is served before the re-render.</summary>
	public volatile TaskCompletionSource? Hold;

	public bool Enabled => true;

	public Task<string> GetVersionAsync(CancellationToken ct) => Task.FromResult(Version);

	public async Task<RenderResult> RenderAsync(RenderState state, CancellationToken ct)
	{
		if (Hold is { } hold)
			await hold.Task.WaitAsync(ct);
		Rendered.Enqueue(state.Slug);
		var firm = state.Config?.Fields.GetValueOrDefault(CoreSiteConfig.FirmName);
		var html = state.Page is null
			? $"<html>not-found|{Version}</html>"
			: $"<html><script type=\"application/json\" id=\"__STATE__\">{{}}</script>{state.Page.Title}" +
			  $"|blocks:{state.Page.Blocks.Count}|menu:{state.Menu?.Items.Count}|firm:{firm}" +
			  $"|alt:{string.Join(',', state.Page.Media.Select(m => m.Alt))}|{Version}</html>";
		return new RenderResult(html, Version);
	}
}

public class RenderingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private sealed record Site(WebApplicationFactory<Program> App, FakeRenderer Renderer, HttpClient Client, string Token)
		: IAsyncDisposable
	{
		public ValueTask DisposeAsync()
		{
			Client.Dispose();
			return App.DisposeAsync();
		}
	}

	private async Task<Site> StartAsync()
	{
		var renderer = new FakeRenderer();
		var app = factory.WithWebHostBuilder(b =>
		{
			b.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Renderer:DebounceMilliseconds"] = "20",
				["Renderer:CheckIntervalSeconds"] = "1",
				["Renderer:PublicBaseUrl"] = "https://site.test",
			}));
			b.ConfigureServices(s =>
			{
				s.RemoveAll<IRendererClient>();
				s.AddSingleton<IRendererClient>(renderer);
			});
		});
		var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

		using var scope = app.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		var staff = new Staff { Email = $"render-{Guid.NewGuid()}@test.com", PasswordHash = "x" };
		db.Staff.Add(staff);
		await db.SaveChangesAsync(Ct);
		var token = app.Services.GetRequiredService<JwtTokenService>()
			.CreateAccessToken(staff.Id, staff.Email, "staff", ["staff"]).Token;
		return new Site(app, renderer, client, token);
	}

	private static async Task<HttpResponseMessage> SendAsync(Site site, HttpMethod method, string url, JsonNode? body = null)
	{
		using var request = new HttpRequestMessage(method, url);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", site.Token);
		if (body is not null)
			request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
		return await site.Client.SendAsync(request, Ct);
	}

	private static async Task<(string Id, string Slug)> CreatePublishedAsync(Site site, string title, int blocks = 1)
	{
		var slug = $"r-{Guid.NewGuid():N}"[..16];
		var create = await SendAsync(site, HttpMethod.Post, "/api/pages", new JsonObject
		{
			["title"] = title,
			["slug"] = slug,
			["blocks"] = Blocks(blocks),
		});
		create.StatusCode.Should().Be(HttpStatusCode.Created);
		var id = (await create.Content.ReadAsStringAsync(Ct)).Split("\"id\":\"")[1].Split('"')[0];
		(await SendAsync(site, HttpMethod.Put, $"/api/pages/{id}", new JsonObject { ["status"] = "published" })).StatusCode.Should().Be(HttpStatusCode.OK);
		return (id, slug);
	}

	private static async Task<string> CreateDraftAsync(Site site, string title, JsonArray? blocks = null)
	{
		var slug = $"r-{Guid.NewGuid():N}"[..16];
		var create = await SendAsync(site, HttpMethod.Post, "/api/pages", new JsonObject
		{
			["title"] = title,
			["slug"] = slug,
			["blocks"] = blocks ?? Blocks(1),
		});
		create.StatusCode.Should().Be(HttpStatusCode.Created);
		return (await create.Content.ReadFromJsonAsync<PageDetail>(Ct))!.Id.ToString();
	}

	private static async Task<MenuDetail> MainMenuAsync(Site site)
	{
		var list = await SendAsync(site, HttpMethod.Get, "/api/menus");
		return (await list.Content.ReadFromJsonAsync<List<MenuDetail>>(Ct))!.Single(m => m.Handle == "main");
	}

	/// <summary>Replaces a menu's items with <paramref name="count"/> URL links.</summary>
	private static async Task SetMenuItemsAsync(Site site, Guid menuId, int count)
	{
		var items = new JsonArray(Enumerable.Range(0, count)
			.Select(i => (JsonNode)new JsonObject
			{
				["id"] = $"m{i}",
				["label"] = $"Link {i}",
				["url"] = $"/link-{i}",
				["children"] = new JsonArray(),
			})
			.ToArray());
		(await SendAsync(site, HttpMethod.Put, $"/api/menus/{menuId}", new JsonObject { ["items"] = items }))
			.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52];

	private static async Task<MediaItem> UploadAsync(Site site, string alt)
	{
		using var form = new MultipartFormDataContent();
		var file = new ByteArrayContent(Png);
		file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
		form.Add(file, "file", "img.png");
		form.Add(new StringContent(alt), "alt");
		using var request = new HttpRequestMessage(HttpMethod.Post, "/api/media") { Content = form };
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", site.Token);
		var response = await site.Client.SendAsync(request, Ct);
		response.StatusCode.Should().Be(HttpStatusCode.Created);
		return (await response.Content.ReadFromJsonAsync<MediaItem>(Ct))!;
	}

	/// <summary>A published page showing an image with <paramref name="mediaId"/>, rendered.</summary>
	private static async Task<string> CreateImagePageAsync(Site site, Guid mediaId, string alt)
	{
		var id = await CreateDraftAsync(site, "Image page", new JsonArray(new JsonObject
		{
			["id"] = "i1",
			["name"] = "image",
			["attributes"] = new JsonObject { ["mediaId"] = mediaId.ToString() },
			["innerBlocks"] = new JsonArray(),
		}));
		var publish = await SendAsync(site, HttpMethod.Put, $"/api/pages/{id}", new JsonObject { ["status"] = "published" });
		var slug = (await publish.Content.ReadFromJsonAsync<PageSummary>(Ct))!.Slug;
		await WaitForAsync(site, $"/api/public/html/{slug}", (_, body) => body.Contains($"alt:{alt}|"));
		return slug;
	}

	/// <summary>Holds every render until the returned source is completed.</summary>
	private static TaskCompletionSource Hold(Site site)
	{
		var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		site.Renderer.Hold = hold;
		return hold;
	}

	private static void Release(Site site, TaskCompletionSource hold)
	{
		site.Renderer.Hold = null;
		hold.SetResult();
	}

	private static bool Marked(string body) => body.Contains($"id=\"__STATE__\" {PublicHtmlController.RefreshAttribute}");

	/// <summary>Gives the worker time to pick up a job (debounce is 20 ms in these tests).</summary>
	private static Task SettleAsync() => Task.Delay(500, Ct);

	private static JsonArray Blocks(int count) => new(Enumerable.Range(0, count)
		.Select(i => (JsonNode)new JsonObject
		{
			["id"] = $"b{i}",
			["name"] = "paragraph",
			["attributes"] = new JsonObject { ["text"] = $"p{i}" },
			["innerBlocks"] = new JsonArray(),
		})
		.ToArray());

	/// <summary>Polls the public HTML endpoint until the body satisfies <paramref name="done"/>.</summary>
	private static async Task<HttpResponseMessage> WaitForAsync(Site site, string path, Func<HttpResponseMessage, string, bool> done)
	{
		var deadline = DateTime.UtcNow.AddSeconds(15);
		while (true)
		{
			var response = await site.Client.GetAsync(path, Ct);
			var body = await response.Content.ReadAsStringAsync(Ct);
			if (done(response, body)) return response;
			if (DateTime.UtcNow > deadline)
				throw new TimeoutException($"{path}: {(int)response.StatusCode} {body}");
			await Task.Delay(50, Ct);
		}
	}

	private static string? Accel(HttpResponseMessage response) =>
		response.Headers.TryGetValues("X-Accel-Redirect", out var v) ? v.Single() : null;

	[Fact]
	public async Task PublishedPage_IsRenderedAndServed_WithEtag()
	{
		await using var site = await StartAsync();
		var (_, slug) = await CreatePublishedAsync(site, "Hello render");

		var response = await WaitForAsync(site, $"/api/public/html/{slug}",
			(r, body) => r.StatusCode == HttpStatusCode.OK && body.Contains("Hello render"));
		response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
		var etag = response.Headers.ETag!;

		using var conditional = new HttpRequestMessage(HttpMethod.Get, $"/api/public/html/{slug}");
		conditional.Headers.IfNoneMatch.Add(etag);
		(await site.Client.SendAsync(conditional, Ct)).StatusCode.Should().Be(HttpStatusCode.NotModified);
	}

	[Fact]
	public async Task EditingPublishedPage_ReRendersIt_AndTitleChangeReRendersOthers()
	{
		await using var site = await StartAsync();
		var (id, slug) = await CreatePublishedAsync(site, "Edit me");
		var (_, other) = await CreatePublishedAsync(site, "Other page");
		await WaitForAsync(site, $"/api/public/html/{other}", (r, body) => body.Contains("Other page"));
		await WaitForAsync(site, $"/api/public/html/{slug}", (r, body) => body.Contains("blocks:1"));

		// content-only autosave (title unchanged): just this page
		await SendAsync(site, HttpMethod.Put, $"/api/pages/{id}", new JsonObject { ["title"] = "Edit me", ["blocks"] = Blocks(3) });
		await WaitForAsync(site, $"/api/public/html/{slug}", (r, body) => body.Contains("blocks:3"));

		site.Renderer.Rendered.Clear();
		await SendAsync(site, HttpMethod.Put, $"/api/pages/{id}", new JsonObject { ["title"] = "Renamed" });
		await WaitForAsync(site, $"/api/public/html/{slug}", (r, body) => body.Contains("Renamed"));
		site.Renderer.Rendered.Should().Contain(other);
	}

	[Fact]
	public async Task Rename_MarksOtherPagesRightAway_ContentEditDoesNot()
	{
		await using var site = await StartAsync();
		var (id, slug) = await CreatePublishedAsync(site, "Rename me");
		var (_, other) = await CreatePublishedAsync(site, "Bystander");
		await WaitForAsync(site, $"/api/public/html/{slug}", (_, body) => body.Contains("Rename me"));
		await WaitForAsync(site, $"/api/public/html/{other}", (_, body) => body.Contains("Bystander") && !Marked(body));

		var hold = Hold(site);
		await SendAsync(site, HttpMethod.Put, $"/api/pages/{id}", new JsonObject { ["blocks"] = Blocks(2) });
		Marked(await site.Client.GetStringAsync($"/api/public/html/{other}", Ct)).Should().BeFalse();

		await SendAsync(site, HttpMethod.Put, $"/api/pages/{id}", new JsonObject { ["title"] = "Renamed" });
		var during = await site.Client.GetStringAsync($"/api/public/html/{other}", Ct);
		during.Should().Contain("Bystander");
		Marked(during).Should().BeTrue();
		Release(site, hold);

		await WaitForAsync(site, $"/api/public/html/{other}", (_, body) => !Marked(body));
	}

	[Fact]
	public async Task AfterAChange_OldHtmlIsNeverServed()
	{
		await using var site = await StartAsync();
		var (id, slug) = await CreatePublishedAsync(site, "Fresh");
		await WaitForAsync(site, $"/api/public/html/{slug}", (_, body) => body.Contains("blocks:1"));

		await SendAsync(site, HttpMethod.Put, $"/api/pages/{id}", new JsonObject { ["blocks"] = Blocks(2) });
		// right away: either the new render or the live shell, not the old HTML
		var response = await site.Client.GetAsync($"/api/public/html/{slug}", Ct);
		(await response.Content.ReadAsStringAsync(Ct)).Should().NotContain("blocks:1");
		if (!(await response.Content.ReadAsStringAsync(Ct)).Contains("blocks:2"))
			Accel(response).Should().Be(PublicHtmlController.PublicShell);
	}

	[Fact]
	public async Task Unpublish_Returns404Immediately_OthersKeepServing()
	{
		await using var site = await StartAsync();
		var (_, a) = await CreatePublishedAsync(site, "Stays");
		var (id, b) = await CreatePublishedAsync(site, "Short lived");
		await WaitForAsync(site, $"/api/public/html/{a}", (_, body) => body.Contains("Stays"));
		await WaitForAsync(site, $"/api/public/html/{b}", (r, _) => r.StatusCode == HttpStatusCode.OK && Accel(r) is null);

		var hold = Hold(site);
		await SendAsync(site, HttpMethod.Put, $"/api/pages/{id}", new JsonObject { ["status"] = "draft" });
		var response = await site.Client.GetAsync($"/api/public/html/{b}", Ct);
		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
		(await response.Content.ReadAsStringAsync(Ct)).Should().NotContain("Short lived");

		var other = await site.Client.GetAsync($"/api/public/html/{a}", Ct);
		Accel(other).Should().BeNull();
		var body = await other.Content.ReadAsStringAsync(Ct);
		body.Should().Contain("Stays");
		// marked right away, not only once the worker picks the job up: the client drops B from the menu
		Marked(body).Should().BeTrue();
		Release(site, hold);
	}

	[Fact]
	public async Task RoutingResponses_ForReservedHomeAndUnknown()
	{
		await using var site = await StartAsync();

		var reserved = await site.Client.GetAsync("/api/public/html/login", Ct);
		Accel(reserved).Should().Be(PublicHtmlController.SpaShell);

		var home = await site.Client.GetAsync("/api/public/html/home", Ct);
		home.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
		home.Headers.Location!.ToString().Should().Be("/");

		var unknown = await WaitForAsync(site, "/api/public/html/does-not-exist",
			(r, body) => r.StatusCode == HttpStatusCode.NotFound && body.Contains("not-found|"));
		unknown.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
	}

	[Fact]
	public async Task RendererVersionChange_ReRendersEverything()
	{
		await using var site = await StartAsync();
		var (_, slug) = await CreatePublishedAsync(site, "Versioned");
		await WaitForAsync(site, $"/api/public/html/{slug}", (_, body) => body.Contains("|v1"));

		site.Renderer.Version = "v2";
		// until re-rendered, v1 HTML is never served (its asset files are gone after a deploy)
		await WaitForAsync(site, $"/api/public/html/{slug}", (r, body) =>
			body.Contains("|v2") || (Accel(r) == PublicHtmlController.PublicShell && !body.Contains("|v1")));
		await WaitForAsync(site, $"/api/public/html/{slug}", (_, body) => body.Contains("|v2"));
	}

	[Fact]
	public async Task WithoutRenderer_PublishedPageFallsBackToShell()
	{
		// plain factory: Renderer:BaseUrl unset → worker off
		using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
		await using var site = await StartAsync();
		var (_, slug) = await CreatePublishedAsync(site, "No renderer");

		var response = await client.GetAsync($"/api/public/html/{slug}", Ct);
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		Accel(response).Should().Be(PublicHtmlController.PublicShell);
	}

	[Fact]
	public async Task MainMenuEdit_KeepsServingOldHtml_ThenReRenders()
	{
		await using var site = await StartAsync();
		var main = await MainMenuAsync(site);
		var (_, slug) = await CreatePublishedAsync(site, "Menu page");
		await SetMenuItemsAsync(site, main.Id, 2);
		var before = await WaitForAsync(site, $"/api/public/html/{slug}", (_, body) => body.Contains("menu:2|") && !Marked(body));
		const int count = 3;

		var hold = Hold(site);
		await SetMenuItemsAsync(site, main.Id, count);
		var during = await site.Client.GetAsync($"/api/public/html/{slug}", Ct);
		during.StatusCode.Should().Be(HttpStatusCode.OK);
		Accel(during).Should().BeNull();
		var body = await during.Content.ReadAsStringAsync(Ct);
		body.Should().Contain("menu:2|");
		Marked(body).Should().BeTrue();
		during.Headers.ETag!.Tag.Should().EndWith("-r\"").And.NotBe(before.Headers.ETag!.Tag);
		Release(site, hold);

		var after = await WaitForAsync(site, $"/api/public/html/{slug}", (_, b) => b.Contains($"menu:{count}|"));
		Marked(await after.Content.ReadAsStringAsync(Ct)).Should().BeFalse();
		after.Headers.ETag!.Tag.Should().NotEndWith("-r\"");
	}

	[Fact]
	public async Task UnchangedPage_HasNoMarker()
	{
		await using var site = await StartAsync();
		var (id, a) = await CreatePublishedAsync(site, "Page A");
		var (_, b) = await CreatePublishedAsync(site, "Page B");
		await WaitForAsync(site, $"/api/public/html/{a}", (_, body) => body.Contains("Page A"));
		var response = await WaitForAsync(site, $"/api/public/html/{b}", (_, body) => body.Contains("Page B"));
		Marked(await response.Content.ReadAsStringAsync(Ct)).Should().BeFalse();

		// a quiet change without its re-render: everything rendered so far is marked
		site.App.Services.GetRequiredService<RenderStatus>().MarkRefreshed();
		await WaitForAsync(site, $"/api/public/html/{a}", (_, body) => Marked(body));

		// A re-rendered after it (content edit) loses the marker, B keeps it
		await SendAsync(site, HttpMethod.Put, $"/api/pages/{id}", new JsonObject { ["blocks"] = Blocks(2) });
		await WaitForAsync(site, $"/api/public/html/{a}", (_, body) => body.Contains("blocks:2") && !Marked(body));
		Marked(await site.Client.GetStringAsync($"/api/public/html/{b}", Ct)).Should().BeTrue();
	}

	[Fact]
	public async Task OtherMenuEdit_RendersNothing()
	{
		await using var site = await StartAsync();
		var (_, slug) = await CreatePublishedAsync(site, "Footer menu");
		await WaitForAsync(site, $"/api/public/html/{slug}", (_, body) => body.Contains("Footer menu"));
		await SettleAsync();
		site.Renderer.Rendered.Clear();

		var create = await SendAsync(site, HttpMethod.Post, "/api/menus", new JsonObject { ["handle"] = $"extra-{Guid.NewGuid():N}"[..20] });
		create.StatusCode.Should().Be(HttpStatusCode.Created);
		var menu = (await create.Content.ReadFromJsonAsync<MenuDetail>(Ct))!;
		await SetMenuItemsAsync(site, menu.Id, 2);
		(await SendAsync(site, HttpMethod.Delete, $"/api/menus/{menu.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

		await SettleAsync();
		site.Renderer.Rendered.Should().BeEmpty();
		Marked(await site.Client.GetStringAsync($"/api/public/html/{slug}", Ct)).Should().BeFalse();
	}

	[Fact]
	public async Task Publish_KeepsServingOtherPagesOldHtml()
	{
		await using var site = await StartAsync();
		var (_, a) = await CreatePublishedAsync(site, "Page A");
		await WaitForAsync(site, $"/api/public/html/{a}", (_, body) => body.Contains("Page A"));
		var id = await CreateDraftAsync(site, "Page B");

		var hold = Hold(site);
		var publish = await SendAsync(site, HttpMethod.Put, $"/api/pages/{id}", new JsonObject { ["status"] = "published" });
		var b = (await publish.Content.ReadFromJsonAsync<PageSummary>(Ct))!.Slug;
		var other = await site.Client.GetAsync($"/api/public/html/{a}", Ct);
		Accel(other).Should().BeNull();
		var body = await other.Content.ReadAsStringAsync(Ct);
		body.Should().Contain("Page A");
		Marked(body).Should().BeTrue();
		Accel(await site.Client.GetAsync($"/api/public/html/{b}", Ct)).Should().Be(PublicHtmlController.PublicShell);
		site.Renderer.Rendered.Clear();
		Release(site, hold);

		await WaitForAsync(site, $"/api/public/html/{b}", (r, body) => Accel(r) is null && body.Contains("Page B"));
		await WaitForAsync(site, $"/api/public/html/{a}", (_, body) => !Marked(body));
		site.Renderer.Rendered.Should().Contain(a);
	}

	[Fact]
	public async Task SiteConfigEdit_KeepsServingOldHtml_ThenReRenders()
	{
		await using var site = await StartAsync();
		var (_, slug) = await CreatePublishedAsync(site, "Config page");
		await WaitForAsync(site, $"/api/public/html/{slug}", (_, body) => body.Contains("Config page"));
		var firm = $"Firm {Guid.NewGuid():N}"[..13];

		var hold = Hold(site);
		var update = await SendAsync(site, HttpMethod.Put, "/api/site-config", new JsonObject
		{
			["fields"] = new JsonObject { [CoreSiteConfig.FirmName] = firm },
			["groups"] = new JsonObject(),
		});
		update.StatusCode.Should().Be(HttpStatusCode.OK);
		var during = await site.Client.GetAsync($"/api/public/html/{slug}", Ct);
		Accel(during).Should().BeNull();
		var body = await during.Content.ReadAsStringAsync(Ct);
		body.Should().Contain("Config page").And.NotContain(firm);
		Marked(body).Should().BeTrue();
		Release(site, hold);

		await WaitForAsync(site, $"/api/public/html/{slug}", (_, b) => b.Contains($"firm:{firm}|") && !Marked(b));
	}

	[Fact]
	public async Task MediaAltEdit_KeepsServingOldHtml_ThenReRenders()
	{
		await using var site = await StartAsync();
		var item = await UploadAsync(site, "Old alt");
		var slug = await CreateImagePageAsync(site, item.Id, "Old alt");

		var hold = Hold(site);
		(await SendAsync(site, HttpMethod.Put, $"/api/media/{item.Id}", new JsonObject { ["alt"] = "New alt" }))
			.StatusCode.Should().Be(HttpStatusCode.OK);
		var during = await site.Client.GetAsync($"/api/public/html/{slug}", Ct);
		Accel(during).Should().BeNull();
		var body = await during.Content.ReadAsStringAsync(Ct);
		body.Should().Contain("alt:Old alt|");
		Marked(body).Should().BeTrue();
		Release(site, hold);

		await WaitForAsync(site, $"/api/public/html/{slug}", (_, b) => b.Contains("alt:New alt|") && !Marked(b));
	}

	[Fact]
	public async Task MediaRenameOnly_RendersNothing()
	{
		await using var site = await StartAsync();
		var item = await UploadAsync(site, "Same alt");
		var slug = await CreateImagePageAsync(site, item.Id, "Same alt");
		await SettleAsync();
		site.Renderer.Rendered.Clear();

		var update = await SendAsync(site, HttpMethod.Put, $"/api/media/{item.Id}",
			new JsonObject { ["alt"] = " Same alt ", ["fileName"] = "renamed.png" });
		update.StatusCode.Should().Be(HttpStatusCode.OK);
		(await update.Content.ReadFromJsonAsync<MediaItem>(Ct))!.FileName.Should().Be("renamed.png");

		await SettleAsync();
		site.Renderer.Rendered.Should().BeEmpty();
		Marked(await site.Client.GetStringAsync($"/api/public/html/{slug}", Ct)).Should().BeFalse();
	}

	[Fact]
	public async Task MediaDelete_KeepsServingOldHtml()
	{
		await using var site = await StartAsync();
		var item = await UploadAsync(site, "Doomed");
		var slug = await CreateImagePageAsync(site, item.Id, "Doomed");

		var hold = Hold(site);
		(await SendAsync(site, HttpMethod.Delete, $"/api/media/{item.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
		var during = await site.Client.GetAsync($"/api/public/html/{slug}", Ct);
		Accel(during).Should().BeNull();
		var body = await during.Content.ReadAsStringAsync(Ct);
		body.Should().Contain("alt:Doomed|");
		Marked(body).Should().BeTrue();
		Release(site, hold);

		await WaitForAsync(site, $"/api/public/html/{slug}", (_, b) => b.Contains("alt:|") && !Marked(b));
	}

	[Fact]
	public async Task ManyPages_AllRenderedAcrossChunks()
	{
		await using var site = await StartAsync();
		var hold = Hold(site);
		var pages = new List<(string Id, string Slug)>();
		for (var i = 0; i < 45; i++)
			pages.Add(await CreatePublishedAsync(site, $"Many {i}"));
		Release(site, hold);

		foreach (var (i, page) in pages.Index())
			await WaitForAsync(site, $"/api/public/html/{page.Slug}",
				(r, body) => Accel(r) is null && body.Contains($"Many {i}|"));
	}
}
