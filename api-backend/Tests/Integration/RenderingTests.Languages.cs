using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using api_backend.Controllers;
using api_backend.Models.Dto;
using api_backend.Services.Cms;
using api_backend.Services.Menus;
using api_backend.Services.Rendering;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace api_backend.Tests.Integration;

/// <summary>Localized site: one stored HTML per page and language, prefixed URLs, the language cookie.</summary>
public partial class RenderingTests
{
	private static readonly CmsConfig Localized = ApiFactory.Cms with
	{
		Site = new CmsSiteMeta { Lang = new CmsSiteLang { Default = "en-US", Localization = ["de-CH", "fr-CH", "it-CH"] } },
	};

	private static async Task<HttpResponseMessage> GetAsync(Site site, string path, string? langCookie = null)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, path);
		if (langCookie is not null)
			request.Headers.Add("Cookie", $"{PublicHtmlController.LangCookie}={langCookie}");
		return await site.Client.SendAsync(request, Ct);
	}

	private static async Task<string> BodyAsync(Site site, string path) =>
		await (await GetAsync(site, path)).Content.ReadAsStringAsync(Ct);

	[Fact]
	public async Task Localized_RendersEveryPageInEveryLanguage_AndServesEachLanguagesRow()
	{
		await using var site = await StartAsync(cms: Localized);
		var (id, slug) = await CreatePublishedAsync(site, "Multi");

		var german = await WaitForAsync(site, $"/api/public/html/de/{slug}",
			(r, body) => r.StatusCode == HttpStatusCode.OK && body.Contains("Multi"));
		(await german.Content.ReadAsStringAsync(Ct)).Should().Contain("|lang:de|");
		(await BodyAsync(site, $"/api/public/html/{slug}")).Should().Contain("|lang:en|");
		(await BodyAsync(site, $"/api/public/html/fr/{slug}")).Should().Contain("|lang:fr|");

		site.Renderer.States.Where(s => s.Slug == slug).Select(s => s.Lang).Should().Contain(["en", "de", "fr", "it"]);
		using var scope = site.App.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		(await db.RenderedPages.Where(r => r.PageId == Guid.Parse(id)).Select(r => r.Lang).ToListAsync(Ct))
			.Should().BeEquivalentTo(["en", "de", "fr", "it"]);
	}

	[Fact]
	public async Task Localized_EachLanguageRendersItsTranslation()
	{
		await using var site = await StartAsync(cms: Localized);
		var (id, slug) = await CreatePublishedAsync(site, "English title");
		(await SendAsync(site, HttpMethod.Put, $"/api/pages/{id}", new JsonObject
		{
			["translations"] = new JsonObject { ["de"] = new JsonObject { ["title"] = "Deutscher Titel" } },
			["status"] = "published",
		})).StatusCode.Should().Be(HttpStatusCode.OK);

		await WaitForAsync(site, $"/api/public/html/de/{slug}", (_, body) => body.Contains("Deutscher Titel|"));
		(await BodyAsync(site, $"/api/public/html/fr/{slug}")).Should().Contain("English title|");
		site.Renderer.States.Where(s => s.Page?.Slug == slug)
			.Should().AllSatisfy(s => s.Page!.Blocks.Should().AllSatisfy(b => b.Translations.Should().BeNull()));
	}

	[Fact]
	public async Task Localized_NotFoundAndScreens_PerLanguage()
	{
		await using var site = await StartAsync(cms: Localized);

		await WaitForAsync(site, "/api/public/html/de/login", (r, body) => r.StatusCode == HttpStatusCode.OK
			&& body.Contains("screen:login|") && body.Contains("|lang:de|"));
		(await BodyAsync(site, "/api/public/html/fr/user/profile"))
			.Should().Contain("screen:user/profile|").And.Contain("|lang:fr|");

		var unknown = await GetAsync(site, "/api/public/html/it/does-not-exist");
		unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
		(await unknown.Content.ReadAsStringAsync(Ct)).Should().Contain("not-found|lang:it|");
		(await BodyAsync(site, "/api/public/html/does-not-exist"))
			.Should().Contain("not-found|lang:en|");
	}

	[Fact]
	public async Task Localized_Routing_PrefixesRedirectsAndCookie()
	{
		await using var site = await StartAsync(cms: Localized);
		var (_, slug) = await CreatePublishedAsync(site, "Routed");
		await WaitForAsync(site, $"/api/public/html/de/{slug}", (r, _) => r.StatusCode == HttpStatusCode.OK && Accel(r) is null);

		// default language's prefix → unprefixed
		var en = await GetAsync(site, $"/api/public/html/en/{slug}?x=1");
		en.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
		en.Headers.Location!.ToString().Should().Be($"/{slug}?x=1");
		(await GetAsync(site, "/api/public/html/en")).Headers.Location!.ToString().Should().Be("/");

		// unknown prefix
		(await GetAsync(site, $"/api/public/html/xx/{slug}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

		// remembered language
		var remembered = await GetAsync(site, $"/api/public/html/{slug}?x=1", "fr");
		remembered.StatusCode.Should().Be(HttpStatusCode.Found);
		remembered.Headers.Location!.ToString().Should().Be($"/fr/{slug}?x=1");
		remembered.Headers.Vary.Should().Contain("Cookie");
		(await GetAsync(site, "/api/public/html/", "de")).Headers.Location!.ToString().Should().Be("/de");
		(await GetAsync(site, "/api/public/html/user/profile", "it")).Headers.Location!.ToString().Should().Be("/it/user/profile");
		// unconfigured or default language in the cookie: served as is
		var es = await GetAsync(site, $"/api/public/html/{slug}", "es");
		es.StatusCode.Should().Be(HttpStatusCode.OK);
		es.Headers.Vary.Should().Contain("Cookie");
		(await GetAsync(site, $"/api/public/html/{slug}", "en")).StatusCode.Should().Be(HttpStatusCode.OK);
		// admin routes never redirect
		Accel(await GetAsync(site, "/api/public/html/admin", "de")).Should().Be(PublicHtmlController.SpaShell);

		var admin = await GetAsync(site, "/api/public/html/de/admin");
		admin.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
		admin.Headers.Location!.ToString().Should().Be("/admin");
		(await GetAsync(site, "/api/public/html/de/home")).Headers.Location!.ToString().Should().Be("/de");
	}

	[Fact]
	public async Task Localized_PrefixAlone_IsThatLanguagesHome()
	{
		await using var site = await StartAsync(cms: Localized);
		using (var scope = site.App.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
			if (!await db.Pages.AnyAsync(p => p.Slug == "home", Ct))
			{
				var id = await CreateDraftAsync(site, "Home");
				(await SendAsync(site, HttpMethod.Put, $"/api/pages/{id}", new JsonObject { ["slug"] = "home", ["status"] = "published" }))
					.StatusCode.Should().Be(HttpStatusCode.OK);
			}
		}

		var home = await WaitForAsync(site, "/api/public/html/de", (r, body) => r.StatusCode == HttpStatusCode.OK && Accel(r) is null);
		(await home.Content.ReadAsStringAsync(Ct)).Should().Contain("|lang:de|");
	}

	[Fact]
	public async Task Localized_MissingLanguageRow_IsRenderedByThePeriodicCheck()
	{
		// what a language newly added to the config looks like: its rows are missing
		await using var site = await StartAsync(cms: Localized);
		var (id, slug) = await CreatePublishedAsync(site, "Added language");
		await WaitForAsync(site, $"/api/public/html/it/{slug}", (r, _) => r.StatusCode == HttpStatusCode.OK && Accel(r) is null);
		using var scope = site.App.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		await db.RenderedPages.Where(r => r.PageId == Guid.Parse(id) && r.Lang == "it").ExecuteDeleteAsync(Ct);

		await WaitForAsync(site, $"/api/public/html/it/{slug}", (r, body) => r.StatusCode == HttpStatusCode.OK
			&& Accel(r) is null && body.Contains("|lang:it|"));
	}

	[Fact]
	public async Task Localized_MenuAndConfig_LoadedOnceForAllLanguages()
	{
		await using var site = await StartAsync(cms: Localized);
		await CreatePublishedAsync(site, "Cached");
		var cache = site.App.Services.GetRequiredService<PublicCache>();
		cache.Invalidate();
		using var scope = site.App.Services.CreateScope();
		var menus = scope.ServiceProvider.GetRequiredService<MenuService>();

		var german = await cache.MenuAsync(menus, "main", "de", Ct);
		// the first miss stored every language: same generation, no load needed
		(await cache.MenuAsync(new MenuService(null!, Localized), "main", "fr", Ct))
			.Should().NotBeNull();
		german.Should().NotBeNull();
	}

	[Fact]
	public async Task Localized_UnconfiguredLanguagesRows_AreDeletedByAFullRender()
	{
		await using var site = await StartAsync(cms: Localized);
		var (id, slug) = await CreatePublishedAsync(site, "Cleanup");
		await WaitForAsync(site, $"/api/public/html/de/{slug}", (r, _) => r.StatusCode == HttpStatusCode.OK && Accel(r) is null);
		using var scope = site.App.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		db.RenderedPages.Add(new() { PageId = Guid.Parse(id), Lang = "es", Html = "x", Title = "x", Slug = slug, RendererVersion = "v1" });
		await db.SaveChangesAsync(Ct);

		site.Renderer.Version = "v2";
		var deadline = DateTime.UtcNow.AddSeconds(15);
		while (await db.RenderedPages.AnyAsync(r => r.PageId == Guid.Parse(id) && r.Lang == "es", Ct))
		{
			DateTime.UtcNow.Should().BeBefore(deadline);
			await Task.Delay(50, Ct);
		}
	}

	[Fact]
	public async Task SingleLanguage_RendersOneRow_NoPrefixesNoRedirectNoVary()
	{
		await using var site = await StartAsync();
		var (id, slug) = await CreatePublishedAsync(site, "Single");
		var page = await WaitForAsync(site, $"/api/public/html/{slug}", (r, _) => r.StatusCode == HttpStatusCode.OK && Accel(r) is null);
		page.Headers.Vary.Should().BeEmpty();
		(await page.Content.ReadAsStringAsync(Ct)).Should().Contain("|lang:en|");

		(await GetAsync(site, $"/api/public/html/de/{slug}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
		var cookie = await GetAsync(site, $"/api/public/html/{slug}", "de");
		cookie.StatusCode.Should().Be(HttpStatusCode.OK);
		cookie.Headers.Vary.Should().BeEmpty();

		using var scope = site.App.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		(await db.RenderedPages.CountAsync(r => r.PageId == Guid.Parse(id), Ct)).Should().Be(1);
	}

	[Fact]
	public async Task Localized_PublicJsonEndpoints_AcceptLang()
	{
		await using var site = await StartAsync(cms: Localized);
		var (_, slug) = await CreatePublishedAsync(site, "Json");

		(await site.Client.GetFromJsonAsync<PublicPage>($"/api/public/pages/{slug}?lang=de", Ct))!.Title.Should().Be("Json");
		(await site.Client.GetFromJsonAsync<PublicStateResponse>($"/api/public/state/{slug}?lang=xx", Ct))!.Page!.Title.Should().Be("Json");
	}
}
