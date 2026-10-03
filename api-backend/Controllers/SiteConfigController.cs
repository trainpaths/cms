using api_backend.Models.Auth.JWT;
using api_backend.Models.Dto;
using api_backend.Services.Site;
using api_backend.Services.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace api_backend.Controllers;

/// <summary>Site config (key/value fields, logo, icon). Staff (the site owners) only.</summary>
[ApiController]
[Route("api/site-config")]
[Authorize(Policy = AuthPolicies.StaffOnly)]
[EnableRateLimiting(RateLimitPolicies.Pages)]
public class SiteConfigController(SiteConfigService siteConfig, IRenderQueue render) : ControllerBase
{
	[HttpGet]
	public async Task<ActionResult<SiteConfigResponse>> Get(CancellationToken ct) =>
		Ok(await siteConfig.GetAsync(ct));

	[HttpPut]
	public async Task<ActionResult<SiteConfigResponse>> Update(UpdateSiteConfigRequest req, CancellationToken ct)
	{
		var result = await siteConfig.UpdateAsync(req, User.GetUserId(), ct);
		if (result.Error is not null)
			return Problem(statusCode: 400, title: "Invalid configuration.", detail: result.Error);
		render.All();
		return Ok(result.Value);
	}
}
