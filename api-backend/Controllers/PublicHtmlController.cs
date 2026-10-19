using api_backend.Models.Auth.JWT;
using api_backend.Models.Pages;
using api_backend.Services.Cms;
using api_backend.Services.Pages;
using api_backend.Services.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace api_backend.Controllers;

/// <summary>
/// Server-rendered public pages and customer screens. nginx proxies <c>/</c>, every single-segment path and
/// <c>/user/{name}</c> here; the response is the stored HTML (<see cref="RenderWorker"/>), or an
/// <c>X-Accel-Redirect</c> telling nginx which static file to serve instead: <c>/index.html</c> (admin SPA) for its
/// routes, <c>/public.html</c> (client-rendered shell) when no current HTML exists yet. Not part of the OpenAPI
/// contract (HTML, not for the generated client).
/// </summary>
[ApiController]
[Route("api/public/html")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Pages)]
[ApiExplorerSettings(IgnoreApi = true)]
public class PublicHtmlController(AppDbContext db, RenderStatus status, CmsConfig cms) : ControllerBase
{
	public const string SpaShell = "/index.html";
	public const string PublicShell = "/public.html";

	/// <summary>On <c>#__STATE__</c> of stored HTML rendered before a quiet change (see <c>entry-client.ts</c>).</summary>
	public const string RefreshAttribute = "data-refresh";

	// renderer not reached yet (startup) or failed
	private const string FallbackNotFound =
		"<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><meta name=\"robots\" content=\"noindex\">" +
		"<title>Page not found</title><body style=\"font-family:sans-serif;text-align:center;padding:32px\">" +
		"<h1>404</h1><p>Page not found</p><a href=\"/\">Go to homepage</a></body></html>";

	// HEAD too: link checkers / crawlers probe with it
	[HttpGet, HttpHead]
	public Task<IActionResult> Home(CancellationToken ct) => ServeAsync("home", ct);

	[HttpGet("{slug}"), HttpHead("{slug}")]
	public async Task<IActionResult> BySlug(string slug, CancellationToken ct)
	{
		slug = slug.ToLowerInvariant();
		if (slug == "home")
			return RedirectPermanent("/");
		if (PublicScreens.Contains(slug))
			return Screen(slug);
		if (PageService.ReservedSlugs.Contains(slug))
			return Accel(SpaShell);
		return await ServeAsync(slug, ct);
	}

	/// <summary>Customer account pages (<c>/user/profile</c>, ...).</summary>
	[HttpGet("user/{name}"), HttpHead("user/{name}")]
	public IActionResult Account(string name) => Screen($"user/{name.ToLowerInvariant()}");

	/// <summary>
	/// A customer screen: the same HTML for everyone (account data loads client-side), rendered with every full
	/// render. Public auth off or unknown → 404 like an unknown page.
	/// </summary>
	private IActionResult Screen(string path)
	{
		if (!cms.PublicAuth || !PublicScreens.Contains(path))
			return Html(status.NotFoundHtml ?? FallbackNotFound, 404);
		// not rendered yet (startup, renderer down) or by an older build (missing assets): the shell renders it
		if (status.Screens is not { } screens || screens.Version != status.CurrentVersion
			|| !screens.Html.TryGetValue(path, out var html))
			return Accel(PublicShell);
		Response.Headers.CacheControl = "no-cache";
		return Html(status.NeedsRefresh(screens.RenderedAt) ? MarkRefresh(html) : html, 200);
	}

	private async Task<IActionResult> ServeAsync(string slug, CancellationToken ct)
	{
		// only HTML of the current renderer build: older builds reference asset files that no longer exist. null
		// version (renderer not reached yet) matches no row → shell
		var version = status.CurrentVersion;
		// left join on the PK (a correlated FirstOrDefault becomes a ROW_NUMBER() over the whole table)
		var page = await db.Pages.AsNoTracking()
			.Where(p => p.Slug == slug && p.Status == PageStatus.Published)
			.LeftJoin(db.RenderedPages.Where(r => r.RendererVersion == version), p => p.Id, r => r.PageId,
				(p, r) => new { p.Id, Html = r == null ? null : r.Html, RenderedAt = r == null ? (DateTimeOffset?)null : r.RenderedAt })
			.FirstOrDefaultAsync(ct);
		if (page is null)
			return Html(status.NotFoundHtml ?? FallbackNotFound, 404);

		if (page.Html is null || page.RenderedAt is not { } renderedAt || !status.IsFresh(page.Id, renderedAt))
			return Accel(PublicShell);

		// rendered before a quiet change (menu, site config, ...): the client refetches those after hydration. The
		// suffix keeps browsers from reusing a marked copy after the re-render, or an unmarked one during it.
		var refresh = status.NeedsRefresh(renderedAt);
		var etag = $"W/\"{renderedAt.UtcTicks:x}{(refresh ? "-r" : "")}\"";
		Response.Headers.ETag = etag;
		Response.Headers.CacheControl = "no-cache";
		if (Request.Headers.IfNoneMatch == etag)
			return StatusCode(304);
		return Html(refresh ? MarkRefresh(page.Html) : page.Html, 200);
	}

	/// <summary><c>#__STATE__</c> is the contract between <c>public.html</c> and the client (first occurrence).</summary>
	private static string MarkRefresh(string html)
	{
		const string state = "id=\"__STATE__\"";
		var at = html.IndexOf(state, StringComparison.Ordinal);
		return at < 0 ? html : html.Insert(at + state.Length, " " + RefreshAttribute);
	}

	private ContentResult Html(string html, int statusCode) =>
		new() { Content = html, ContentType = "text/html; charset=utf-8", StatusCode = statusCode };

	private EmptyResult Accel(string path)
	{
		Response.Headers["X-Accel-Redirect"] = path;
		return new EmptyResult();
	}
}
