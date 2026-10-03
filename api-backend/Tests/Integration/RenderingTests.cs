using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using api_backend.Controllers;
using api_backend.Models.Auth;
using api_backend.Services.Auth.JWT;
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

	public bool Enabled => true;

	public Task<string> GetVersionAsync(CancellationToken ct) => Task.FromResult(Version);

	public Task<RenderResult> RenderAsync(RenderState state, CancellationToken ct)
	{
		Rendered.Enqueue(state.Slug);
		var html = state.Page is null
			? $"<html>not-found|{Version}</html>"
			: $"<html>{state.Page.Title}|blocks:{state.Page.Blocks.Count}|menu:{state.Menu?.Items.Count}|{Version}</html>";
		return Task.FromResult(new RenderResult(html, Version));
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
		(await SendAsync(site, HttpMethod.Post, $"/api/pages/{id}/publish")).StatusCode.Should().Be(HttpStatusCode.OK);
		return (id, slug);
	}

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
	public async Task UnpublishedPage_Is404Immediately()
	{
		await using var site = await StartAsync();
		var (id, slug) = await CreatePublishedAsync(site, "Short lived");
		await WaitForAsync(site, $"/api/public/html/{slug}", (r, _) => r.StatusCode == HttpStatusCode.OK);

		await SendAsync(site, HttpMethod.Post, $"/api/pages/{id}/unpublish");
		var response = await site.Client.GetAsync($"/api/public/html/{slug}", Ct);
		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
		(await response.Content.ReadAsStringAsync(Ct)).Should().NotContain("Short lived");
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
}
