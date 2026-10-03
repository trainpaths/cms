using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using api_backend.Models.Auth;
using api_backend.Models.Dto;
using api_backend.Models.Menus;
using api_backend.Models.Pages;
using api_backend.Services.Auth.JWT;
using api_backend.Services.Cms;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace api_backend.Tests.Integration;

/// <summary>
/// Behaviour driven by the instance config (cms.config.json): locked/template pages, footer links, public auth,
/// the instance endpoint. Own container (class fixture); the fresh-install seed stays off (ApiFactory).
/// </summary>
public class InstanceConfigTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private static readonly CmsConfig Cms = CmsConfigLoader.Parse("""
		{
			"publicAuth": false,
			"blocks": { "exclude": ["list"] },
			"pages": [
				{ "slug": "home", "title": "Home" },
				{ "slug": "imprint", "title": "Imprint", "template": "imprint", "footer": true,
				  "blocks": [{ "id": "p1", "name": "paragraph", "attributes": { "text": "Hi" }, "innerBlocks": [] }] },
				{ "slug": "terms", "title": "Terms", "locked": true, "footer": true },
				{ "slug": "contact", "title": "Contact", "template": "contact", "editable": false }
			]
		}
		""");

	private WebApplicationFactory<Program> App() => factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
	{
		s.RemoveAll<CmsConfig>();
		s.AddSingleton(Cms);
	}));

	[Fact]
	public async Task Startup_SeedsLockedPagesOnly_WhenNotAFreshInstall()
	{
		await using var app = App();
		using var scope = app.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

		var slugs = await db.Pages.Select(p => p.Slug).ToListAsync(Ct);
		slugs.Should().Contain(["imprint", "terms", "contact"]).And.NotContain("home");
		var imprint = await db.Pages.SingleAsync(p => p.Slug == "imprint", Ct);
		imprint.Status.Should().Be(PageStatus.Published);
		imprint.Blocks.Single().Id.Should().Be("p1");
	}

	[Fact]
	public async Task LockedPage_KeepsItsSlugAndCannotBeDeleted_ButTitleAndStatusChange()
	{
		await using var app = App();
		using var client = app.CreateCookielessClient();
		var token = await StaffTokenAsync(app);
		var terms = await PageAsync(app, "terms");

		var list = await Send(client, HttpMethod.Get, "/api/pages", token).ReadAs<List<PageSummary>>();
		list.Single(p => p.Slug == "terms").Locked.Should().BeTrue();

		var slugChange = await Send(client, HttpMethod.Put, $"/api/pages/{terms.Id}", token, new JsonObject { ["slug"] = "tos" });
		slugChange.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		(await Send(client, HttpMethod.Delete, $"/api/pages/{terms.Id}", token)).StatusCode
			.Should().Be(HttpStatusCode.BadRequest);

		var rename = await Send(client, HttpMethod.Put, $"/api/pages/{terms.Id}", token, new JsonObject { ["title"] = "Terms of use" })
			.ReadAs<PageDetail>();
		rename.Title.Should().Be("Terms of use");
		rename.Locked.Should().BeTrue();
		rename.Template.Should().BeNull();
		rename.Editable.Should().BeTrue();
	}

	[Fact]
	public async Task TemplatePage_ExposesItsTemplate_AndNonEditablePagesRejectBlocks()
	{
		await using var app = App();
		using var client = app.CreateCookielessClient();
		var token = await StaffTokenAsync(app);

		var imprint = await client.GetFromJsonAsync<PublicPage>("/api/public/pages/imprint", Ct);
		imprint!.Template.Should().Be("imprint");

		var contact = await PageAsync(app, "contact");
		var detail = await Send(client, HttpMethod.Get, $"/api/pages/{contact.Id}", token).ReadAs<PageDetail>();
		detail.Template.Should().Be("contact");
		detail.Editable.Should().BeFalse();
		detail.Locked.Should().BeTrue();

		var blocks = await Send(client, HttpMethod.Put, $"/api/pages/{contact.Id}", token, new JsonObject { ["blocks"] = new JsonArray() });
		blocks.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		(await Send(client, HttpMethod.Put, $"/api/pages/{contact.Id}", token, new JsonObject { ["title"] = "Reach us" }))
			.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task FooterLinks_ArePublishedFooterPagesInConfigOrder()
	{
		await using var app = App();
		using var client = app.CreateCookielessClient();
		await using (var scope = app.Services.CreateAsyncScope())
		{
			// title may have been renamed by another test
			var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
			await db.Pages.Where(p => p.Slug == "terms")
				.ExecuteUpdateAsync(s => s.SetProperty(p => p.Title, "Terms").SetProperty(p => p.Status, PageStatus.Published), Ct);
		}

		var config = await client.GetFromJsonAsync<SiteConfigResponse>("/api/public/site-config", Ct);
		config!.FooterLinks.Should().Equal(new FooterLink("Imprint", "imprint"), new FooterLink("Terms", "terms"));
	}

	[Fact]
	public async Task PublicAuthOff_CustomerApiIsGone_AndTheInstanceEndpointSaysSo()
	{
		await using var app = App();
		using var client = app.CreateCookielessClient();

		var register = await client.PostAsJsonAsync("/api/auth/customer/register",
			new RegisterRequest("someone@test.com", "password123", "Someone"), Ct);
		register.StatusCode.Should().Be(HttpStatusCode.NotFound);
		(await client.PostAsJsonAsync("/api/auth/customer/login", new LoginRequest("someone@test.com", "password123"), Ct))
			.StatusCode.Should().Be(HttpStatusCode.NotFound);

		var instance = await client.GetFromJsonAsync<InstanceConfig>("/api/public/instance", Ct);
		instance!.PublicAuth.Should().BeFalse();
		instance.ExcludedBlocks.Should().Equal("list");

		// the default test app has it on
		using var defaultClient = factory.CreateCookielessClient();
		(await defaultClient.GetFromJsonAsync<InstanceConfig>("/api/public/instance", Ct))!.PublicAuth.Should().BeTrue();
	}

	[Fact]
	public async Task MainMenuSeed_LeavesOutLogin_WhenPublicAuthIsOff()
	{
		await using (var scope = factory.Services.CreateAsyncScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
			await db.Menus.Where(m => m.Handle == Menu.MainHandle).ExecuteDeleteAsync(Ct);
		}

		await using var app = App();
		await using var check = app.Services.CreateAsyncScope();
		var main = await check.ServiceProvider.GetRequiredService<AppDbContext>().Menus
			.SingleAsync(m => m.Handle == Menu.MainHandle, Ct);
		main.Items.Select(i => i.Url).Should().Equal("/");
	}

	private static async Task<Page> PageAsync(WebApplicationFactory<Program> app, string slug)
	{
		await using var scope = app.Services.CreateAsyncScope();
		return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Pages.AsNoTracking()
			.SingleAsync(p => p.Slug == slug, Ct);
	}

	/// <summary>Pages reference their author, so the staff row must exist.</summary>
	private static async Task<string> StaffTokenAsync(WebApplicationFactory<Program> app)
	{
		await using var scope = app.Services.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		var staff = new Staff
		{
			Email = $"editor-{Guid.NewGuid()}@test.com",
			PasswordHash = scope.ServiceProvider.GetRequiredService<PasswordHashService>().Hash("password123"),
		};
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

file static class ResponseExtensions
{
	public static async Task<T> ReadAs<T>(this Task<HttpResponseMessage> response)
	{
		var message = await response;
		message.EnsureSuccessStatusCode();
		return (await message.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken))!;
	}
}
