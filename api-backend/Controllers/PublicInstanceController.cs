using api_backend.Models.Auth.JWT;
using api_backend.Models.Dto;
using api_backend.Services.Cms;
using api_backend.Services.Translation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace api_backend.Controllers;

/// <summary>Instance config for the frontend (the API is the only runtime reader of <c>cms.config.json</c>).</summary>
[ApiController]
[Route("api/public/instance")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Pages)]
public class PublicInstanceController(CmsConfig cms, IOptions<TranslationOptions> translation) : ControllerBase
{
	[HttpGet]
	public InstanceConfig Get() =>
		new(cms.PublicAuth, cms.Blocks.Exclude, cms.SiteConfigSchema(), translation.Value.Enabled);
}
