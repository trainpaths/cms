using api_backend.Models.Auth.JWT;
using api_backend.Models.Dto;
using api_backend.Services.Cms;
using api_backend.Services.Rendering;
using api_backend.Services.Site;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace api_backend.Controllers;

/// <summary>Anonymous read of the site config (public footer, favicon).</summary>
[ApiController]
[Route("api/public/site-config")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Pages)]
public class PublicSiteConfigController(SiteConfigService siteConfig, PublicCache cache, CmsConfig cms) : ControllerBase
{
	/// <param name="lang">language of the footer link titles (unknown or missing → the default language)</param>
	[HttpGet]
	public async Task<ActionResult<SiteConfigResponse>> Get([FromQuery] string? lang, CancellationToken ct) =>
		Ok(await cache.SiteConfigAsync(siteConfig, cms.Site.Lang.Resolve(lang), ct));
}
