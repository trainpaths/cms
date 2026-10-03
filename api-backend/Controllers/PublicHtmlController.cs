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
		var page = await db.Pages.AsNoTracking()
			.Where(p => p.Slug == slug && p.Status == PageStatus.Published)
			.Select(p => new { p.Id })
			.FirstOrDefaultAsync(ct);
		if (page is null)
			return Html(status.NotFoundHtml ?? FallbackNotFound, 404);

		// only HTML of the current renderer build: older builds reference asset files that no longer exist
		var version = status.CurrentVersion;
		var rendered = version is null
			? null
			: await db.RenderedPages.AsNoTracking()
				.Where(r => r.PageId == page.Id && r.RendererVersion == version)
				.Select(r => new { r.Html, r.RenderedAt })
				.FirstOrDefaultAsync(ct);
		if (rendered is null || !status.IsFresh(page.Id, rendered.RenderedAt))
			return Accel(PublicShell);

		var etag = $"W/\"{rendered.RenderedAt.UtcTicks:x}\"";
		Response.Headers.ETag = etag;
		Response.Headers.CacheControl = "no-cache";
		if (Request.Headers.IfNoneMatch == etag)
			return StatusCode(304);
		return Html(rendered.Html, 200);
	}

	private ContentResult Html(string html, int statusCode) =>
		new() { Content = html, ContentType = "text/html; charset=utf-8", StatusCode = statusCode };

	private EmptyResult Accel(string path)
	{
		Response.Headers["X-Accel-Redirect"] = path;
		return new EmptyResult();
	}
}
