using api_backend.Models.Auth.JWT;
using api_backend.Models.Pages;
using api_backend.Services.Pages;
using api_backend.Services.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace api_backend.Controllers;

/// <summary>
/// Server-rendered public pages. nginx proxies <c>/</c> and every single-segment path here; the response is
/// the stored HTML (<see cref="RenderWorker"/>), or an <c>X-Accel-Redirect</c> telling nginx which static file
/// to serve instead: <c>/index.html</c> (admin SPA) for app routes, <c>/public.html</c> (client-rendered shell)
/// when no current HTML exists yet. Not part of the OpenAPI contract (HTML, not for the generated client).
/// </summary>
[ApiController]
[Route("api/public/html")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Pages)]
[ApiExplorerSettings(IgnoreApi = true)]
public class PublicHtmlController(AppDbContext db, RenderStatus status) : ControllerBase
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
		if (PageService.ReservedSlugs.Contains(slug))
			return Accel(SpaShell);
		return await ServeAsync(slug, ct);
	}

	private async Task<IActionResult> ServeAsync(string slug, CancellationToken ct)
	{
		// only HTML of the current renderer build: older builds reference asset files that no longer exist. null
		// version (renderer not reached yet) matches no row → shell
		var version = status.CurrentVersion;
		var page = await db.Pages.AsNoTracking()
			.Where(p => p.Slug == slug && p.Status == PageStatus.Published)
			.Select(p => new
			{
				p.Id,
				Rendered = db.RenderedPages
					.Where(r => r.PageId == p.Id && r.RendererVersion == version)
					.Select(r => new { r.Html, r.RenderedAt })
					.FirstOrDefault(),
			})
			.FirstOrDefaultAsync(ct);
		if (page is null)
			return Html(status.NotFoundHtml ?? FallbackNotFound, 404);

		var rendered = page.Rendered;
		if (rendered is null || !status.IsFresh(page.Id, rendered.RenderedAt))
			return Accel(PublicShell);

		// rendered before a quiet change (menu, site config, ...): the client refetches those after hydration. The
		// suffix keeps browsers from reusing a marked copy after the re-render, or an unmarked one during it.
		var refresh = status.NeedsRefresh(rendered.RenderedAt);
		var etag = $"W/\"{rendered.RenderedAt.UtcTicks:x}{(refresh ? "-r" : "")}\"";
		Response.Headers.ETag = etag;
		Response.Headers.CacheControl = "no-cache";
		if (Request.Headers.IfNoneMatch == etag)
			return StatusCode(304);
		return Html(refresh ? MarkRefresh(rendered.Html) : rendered.Html, 200);
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
