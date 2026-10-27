using api_backend.Models.Auth.JWT;
using api_backend.Models.Dto;
using api_backend.Models.Menus;
using api_backend.Services.Cms;
using api_backend.Services.Menus;
using api_backend.Services.Pages;
using api_backend.Services.Rendering;
using api_backend.Services.Site;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace api_backend.Controllers;

/// <summary>
/// Everything the public client needs in one call: the page (when a slug is given), the main menu and the site
/// config. Used by the client-rendered shell, the <c>data-refresh</c> refetch (no slug) and the dev SSR.
/// </summary>
[ApiController]
[Route("api/public/state")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Pages)]
public class PublicStateController(
	PageService pages, MenuService menus, SiteConfigService siteConfig, PublicCache cache, CmsConfig cms) : ControllerBase
{
	/// <summary><c>Page</c> is null for an unknown or unpublished slug (and without a slug).</summary>
	/// <param name="lang">language code (unknown or missing → the default language)</param>
	[HttpGet, HttpGet("{slug}")]
	public async Task<ActionResult<PublicStateResponse>> Get(string? slug, [FromQuery] string? lang, CancellationToken ct)
	{
		var page = slug is null ? null : await pages.GetPublishedBySlugAsync(slug, lang, ct);
		var code = cms.Site.Lang.Resolve(lang);
		var menu = await cache.MenuAsync(menus, Menu.MainHandle, code, ct);
		var config = await cache.SiteConfigAsync(siteConfig, code, ct);
		return Ok(new PublicStateResponse(page, menu, config));
	}
}
