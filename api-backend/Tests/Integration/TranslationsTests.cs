using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using api_backend.Models.Auth;
using api_backend.Models.Dto;
using api_backend.Services.Auth.JWT;
using api_backend.Services.Cms;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace api_backend.Tests.Integration;

/// <summary>Translated page content on a localized site: editor round trip, publish, localized public reads.</summary>
public class TranslationsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private static readonly CmsConfig Cms = CmsConfigLoader.Parse("""
		{
			"publicAuth": true,
			"site": { "lang": { "default": "en-US", "localization": ["de-CH", "fr-CH"] } },
			"pages": [{ "slug": "legal", "title": "Legal", "footer": true }]
		}
		""");

	private WebApplicationFactory<Program> App() => factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
	{
		s.RemoveAll<CmsConfig>();
		s.AddSingleton(Cms);
	}));

	private static JsonObject Paragraph(string text, JsonObject? translations = null)
	{
		var block = new JsonObject
		{
			["id"] = "p1",
			["name"] = "paragraph",
			["attributes"] = new JsonObject { ["text"] = text, ["alignment"] = "left" },
			["innerBlocks"] = new JsonArray(),
		};
		if (translations is not null) block["translations"] = translations;
		return block;
	}

	[Fact]
	public async Task Translations_RoundTrip_Publish_AndLocalizedPublicReads()
	{
		await using var app = App();
		using var client = app.CreateCookielessClient();
		var token = await StaffTokenAsync(app);
		var slug = $"t-{Guid.NewGuid():N}"[..12];

		var create = await Send(client, HttpMethod.Post, "/api/pages", token, new JsonObject { ["title"] = "About", ["slug"] = slug });
		var id = (await create.Content.ReadFromJsonAsync<PageDetail>(Ct))!.Id;
		var put = await Send(client, HttpMethod.Put, $"/api/pages/{id}", token, new JsonObject
		{
			["blocks"] = new JsonArray(Paragraph("Hello", new JsonObject
			{
				["de"] = new JsonObject { ["text"] = "Hallo" },
				["fr"] = new JsonObject { ["text"] = "" },
				["it"] = new JsonObject(),
			})),
			["translations"] = new JsonObject { ["de"] = new JsonObject { ["title"] = " Über uns ", ["metaTitle"] = "Team" } },
			["status"] = "published",
		});
		put.StatusCode.Should().Be(HttpStatusCode.OK);

		var detail = (await (await Send(client, HttpMethod.Get, $"/api/pages/{id}", token)).Content.ReadFromJsonAsync<PageDetail>(Ct))!;
		detail.Translations["de"].Should().Be(new Models.Pages.PageTextTranslation("Über uns", "Team", ""));
		detail.Blocks[0].Translations!.Keys.Should().BeEquivalentTo(["de", "fr"]); // empty language dropped

		var german = await client.GetFromJsonAsync<PublicPage>($"/api/public/pages/{slug}?lang=de", Ct);
		german!.Title.Should().Be("Über uns");
		german.MetaTitle.Should().Be("Team");
		german.Blocks[0].Attributes["text"].GetString().Should().Be("Hallo");
		german.Blocks[0].Attributes["alignment"].GetString().Should().Be("left");

		// empty translation → default; default language: no overlay
		(await client.GetFromJsonAsync<PublicPage>($"/api/public/pages/{slug}?lang=fr", Ct))!.Blocks[0].Attributes["text"]
			.GetString().Should().Be("Hello");
		var english = await client.GetStringAsync($"/api/public/pages/{slug}", Ct);
		english.Should().Contain("\"Hello\"").And.NotContain("translations").And.NotContain("Hallo");
		(await client.GetStringAsync($"/api/public/state/{slug}?lang=de", Ct)).Should().NotContain("translations");
	}

	[Fact]
	public async Task TranslationOnlyEdit_IsAnUnpublishedChange()
	{
		await using var app = App();
		using var client = app.CreateCookielessClient();
		var token = await StaffTokenAsync(app);
		var create = await Send(client, HttpMethod.Post, "/api/pages", token, new JsonObject { ["title"] = "Snap" });
		var id = (await create.Content.ReadFromJsonAsync<PageDetail>(Ct))!.Id;
		await Send(client, HttpMethod.Put, $"/api/pages/{id}", token, new JsonObject { ["status"] = "published" });

		var edit = await Send(client, HttpMethod.Put, $"/api/pages/{id}", token, new JsonObject
		{
			["translations"] = new JsonObject { ["de"] = new JsonObject { ["title"] = "Schnappschuss" } },
		});
		(await edit.Content.ReadFromJsonAsync<PageSummary>(Ct))!.HasUnpublishedChanges.Should().BeTrue();

		var back = await Send(client, HttpMethod.Put, $"/api/pages/{id}", token, new JsonObject { ["translations"] = new JsonObject() });
		(await back.Content.ReadFromJsonAsync<PageSummary>(Ct))!.HasUnpublishedChanges.Should().BeFalse();
	}

	[Fact]
	public async Task FooterLinksAndMenus_ShowTranslatedTitles()
	{
		await using var app = App();
		using var client = app.CreateCookielessClient();
		var token = await StaffTokenAsync(app);
		// the config's footer page (the fresh-install seed is off in tests)
		var create = await Send(client, HttpMethod.Post, "/api/pages", token, new JsonObject { ["title"] = "Legal", ["slug"] = "legal" });
		var legalId = (await create.Content.ReadFromJsonAsync<PageDetail>(Ct))!.Id;

		var put = await Send(client, HttpMethod.Put, $"/api/pages/{legalId}", token, new JsonObject
		{
			["translations"] = new JsonObject { ["de"] = new JsonObject { ["title"] = "Rechtliches" } },
			["status"] = "published",
		});
		put.StatusCode.Should().Be(HttpStatusCode.OK);

		(await client.GetFromJsonAsync<SiteConfigResponse>("/api/public/site-config?lang=de", Ct))!.FooterLinks
			.Single(l => l.Slug == "legal").Title.Should().Be("Rechtliches");
		(await client.GetFromJsonAsync<SiteConfigResponse>("/api/public/site-config", Ct))!.FooterLinks
			.Single(l => l.Slug == "legal").Title.Should().Be("Legal");
		(await client.GetFromJsonAsync<PublicStateResponse>("/api/public/state?lang=de", Ct))!.Config.FooterLinks
			.Single(l => l.Slug == "legal").Title.Should().Be("Rechtliches");
	}

	[Theory]
	[InlineData("""{ "DE": { "text": "x" } }""")]
	[InlineData("""{ "de": { "text": { "nested": 1 } } }""")]
	[InlineData("""{ "de": null }""")]
	public async Task InvalidBlockTranslations_Are400(string translations)
	{
		await using var app = App();
		using var client = app.CreateCookielessClient();
		var token = await StaffTokenAsync(app);
		var block = Paragraph("x");
		block["translations"] = JsonNode.Parse(translations);
		var create = await Send(client, HttpMethod.Post, "/api/pages", token, new JsonObject
		{
			["title"] = "Bad",
			["blocks"] = new JsonArray(block),
		});
		create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	private static async Task<string> StaffTokenAsync(WebApplicationFactory<Program> app)
	{
		await using var scope = app.Services.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		var staff = new Staff { Email = $"tr-{Guid.NewGuid()}@test.com", PasswordHash = "x" };
		db.Staff.Add(staff);
		await db.SaveChangesAsync(Ct);
		return app.Services.GetRequiredService<JwtTokenService>()
			.CreateAccessToken(staff.Id, staff.Email, "staff", ["staff"]).Token;
	}

	private static Task<HttpResponseMessage> Send(
		HttpClient client, HttpMethod method, string url, string token, JsonNode? body = null)
	{
		var request = new HttpRequestMessage(method, url);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		if (body is not null)
			request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
		return client.SendAsync(request, Ct);
	}
}
