using api_backend.Models.Auth.JWT;
using api_backend.Models.Dto;
using api_backend.Services.Cms;
using api_backend.Services.Menus;
using api_backend.Services.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace api_backend.Controllers;

/// <summary>Anonymous read of a menu by handle (public navigation), links resolved to published pages.</summary>
[ApiController]
[Route("api/public/menus")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Pages)]
public class PublicMenusController(MenuService menus, PublicCache cache, CmsConfig cms) : ControllerBase
{
	/// <param name="lang">language of page titles (unknown or missing → the default language)</param>
	[HttpGet("{handle}")]
	public async Task<ActionResult<PublicMenu>> Get(string handle, [FromQuery] string? lang, CancellationToken ct)
	{
		var menu = await cache.MenuAsync(menus, handle, cms.Site.Lang.Resolve(lang), ct);
		return menu is null ? Problem(statusCode: 404, title: "Menu not found.") : Ok(menu);
	}
}
