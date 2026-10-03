using api_backend.Models.Auth.JWT;
using api_backend.Models.Dto;
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
public class PublicSiteConfigController(SiteConfigService siteConfig) : ControllerBase
{
	[HttpGet]
	public async Task<ActionResult<SiteConfigResponse>> Get(CancellationToken ct) =>
		Ok(await siteConfig.GetAsync(ct));
}
