using api_backend.Models.Auth.JWT;
using api_backend.Models.Pages;
using api_backend.Services.Cms;
using api_backend.Services.Pages;
using api_backend.Services.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace api_backend.Controllers;

/// <summary>
/// Server-rendered public pages and customer screens. nginx proxies <c>/</c>, every single-segment path and
/// <c>/user/{name}</c> here; the response is the stored HTML (<see cref="RenderWorker"/>), or an
/// <c>X-Accel-Redirect</c> telling nginx which static file to serve instead: <c>/index.html</c> (admin SPA) for its
/// routes, <c>/public.html</c> (client-rendered shell) when no current HTML exists yet. Not part of the OpenAPI
/// contract (HTML, not for the generated client).
/// <para>Localized site: a non-default language code prefixes the path (<c>/de</c>, <c>/de/about</c>,
/// <c>/de/user/profile</c>); unprefixed = the default language, redirected to the <c>cms_lang</c> cookie's language
/// when it names another configured one. Single-language site: no prefixes, no redirect.</para>
/// </summary>
[ApiController]
[Route("api/public/html")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Pages)]
[ApiExplorerSettings(IgnoreApi = true)]
public class PublicHtmlController(AppDbContext db, RenderStatus status, CmsConfig cms) : ControllerBase, IActionFilter
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

	/// <summary>Language cookie written by the public/admin language dropdown (<c>src/i18n/languages.ts</c>).</summary>
	public const string LangCookie = "cms_lang";

	private CmsSiteLang Lang => cms.Site.Lang;

	// HEAD too: link checkers / crawlers probe with it
	[HttpGet, HttpHead]
	public async Task<IActionResult> Home(CancellationToken ct) =>
		RememberedLanguage("") ?? await ServeAsync("home", Lang.DefaultCode, ct);

	[HttpGet("{slug}"), HttpHead("{slug}")]
	public async Task<IActionResult> BySlug(string slug, CancellationToken ct)
	{
		slug = slug.ToLowerInvariant();
		if (Lang.IsPrefix(slug))
			return await ServeAsync("home", slug, ct);
		if (Lang.IsLocalized && slug == Lang.DefaultCode)
			return Permanent("/");
		if (slug == "home")
			return RedirectPermanent("/");
		if (PageService.ReservedSlugs.Contains(slug) && !PublicScreens.Contains(slug))
			return Accel(SpaShell);
		if (RememberedLanguage(slug) is { } redirect)
			return redirect;
		return PublicScreens.Contains(slug) ? Screen(slug, Lang.DefaultCode) : await ServeAsync(slug, Lang.DefaultCode, ct);
	}

	/// <summary>Customer account pages (<c>/user/profile</c>, ...).</summary>
	[HttpGet("user/{name}"), HttpHead("user/{name}")]
	public IActionResult Account(string name)
	{
		var path = $"user/{name.ToLowerInvariant()}";
		return RememberedLanguage(path) ?? Screen(path, Lang.DefaultCode);
	}

	/// <summary><c>/de/about</c>; the default language's prefix (<c>/en/about</c>) → the unprefixed path.</summary>
	[HttpGet("{lang}/{slug}"), HttpHead("{lang}/{slug}")]
	public async Task<IActionResult> Prefixed(string lang, string slug, CancellationToken ct)
	{
		lang = lang.ToLowerInvariant();
		slug = slug.ToLowerInvariant();
		if (Lang.IsLocalized && lang == Lang.DefaultCode)
			return Permanent($"/{slug}");
		if (!Lang.IsPrefix(lang))
			return NotFound(Lang.DefaultCode);
		if (slug == "home")
			return Permanent($"/{lang}");
		if (PublicScreens.Contains(slug))
			return Screen(slug, lang);
		// admin routes have no language: /de/admin → /admin
		if (PageService.ReservedSlugs.Contains(slug))
			return Permanent($"/{slug}");
		return await ServeAsync(slug, lang, ct);
	}

	[HttpGet("{lang}/user/{name}"), HttpHead("{lang}/user/{name}")]
	public IActionResult PrefixedAccount(string lang, string name)
	{
		lang = lang.ToLowerInvariant();
		var path = $"user/{name.ToLowerInvariant()}";
		if (Lang.IsLocalized && lang == Lang.DefaultCode)
			return Permanent($"/{path}");
		return Lang.IsPrefix(lang) ? Screen(path, lang) : NotFound(Lang.DefaultCode);
	}

	/// <summary>
	/// Unprefixed page/screen/home path (<paramref name="path"/> without the leading slash) visited with a
	/// <c>cms_lang</c> cookie naming another configured language → 302 to that language's path (query kept).
	/// </summary>
	private IActionResult? RememberedLanguage(string path)
	{
		if (!Lang.IsLocalized || Request.Cookies[LangCookie] is not { } code || !Lang.IsPrefix(code))
			return null;
		return Redirect((path.Length == 0 ? $"/{code}" : $"/{code}/{path}") + Request.QueryString);
	}

	// every response (redirects, 404s too): stored HTML is no-cache already; this keeps shared caches from serving one
	// language for every cookie
	[NonAction]
	public void OnActionExecuting(ActionExecutingContext context)
	{
		if (Lang.IsLocalized)
			Response.Headers.Vary = "Cookie";
	}

	[NonAction]
	public void OnActionExecuted(ActionExecutedContext context)
	{
	}

	private RedirectResult Permanent(string path) => RedirectPermanent(path + Request.QueryString);

	private ContentResult NotFound(string code) =>
		Html(status.NotFoundHtml?.GetValueOrDefault(code) ?? FallbackNotFound, 404);

	/// <summary>
	/// A customer screen: the same HTML for everyone (account data loads client-side), rendered with every full
	/// render. Public auth off or unknown → 404 like an unknown page.
	/// </summary>
	private IActionResult Screen(string path, string code)
	{
		if (!cms.PublicAuth || !PublicScreens.Contains(path))
			return NotFound(code);
		// not rendered yet (startup, renderer down) or by an older build (missing assets): the shell renders it
		if (status.Screens is not { } screens || screens.Version != status.CurrentVersion
			|| !screens.Html.TryGetValue(RenderedScreens.Key(code, path), out var html))
			return Accel(PublicShell);
		Response.Headers.CacheControl = "no-cache";
		return Html(status.NeedsRefresh(screens.RenderedAt) ? MarkRefresh(html) : html, 200);
	}

	private async Task<IActionResult> ServeAsync(string slug, string code, CancellationToken ct)
	{
		// only HTML of the current renderer build: older builds reference asset files that no longer exist. null
		// version (renderer not reached yet) matches no row → shell
		var version = status.CurrentVersion;
		// left join on the PK (a correlated FirstOrDefault becomes a ROW_NUMBER() over the whole table)
		var page = await db.Pages.AsNoTracking()
			.Where(p => p.Slug == slug && p.Status == PageStatus.Published)
			.LeftJoin(db.RenderedPages.Where(r => r.RendererVersion == version && r.Lang == code), p => p.Id, r => r.PageId,
				(p, r) => new { p.Id, Html = r == null ? null : r.Html, RenderedAt = r == null ? (DateTimeOffset?)null : r.RenderedAt })
			.FirstOrDefaultAsync(ct);
		if (page is null)
			return NotFound(code);

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
