using api_backend.Models.Auth.JWT;
using api_backend.Models.Dto;
using api_backend.Services.Menus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace api_backend.Controllers;

/// <summary>Anonymous read of a menu by handle (public navigation), links resolved to published pages.</summary>
[ApiController]
[Route("api/public/menus")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Pages)]
public class PublicMenusController(MenuService menus) : ControllerBase
{
	[HttpGet("{handle}")]
	public async Task<ActionResult<PublicMenu>> Get(string handle, CancellationToken ct)
	{
		var menu = await menus.GetPublicAsync(handle, ct);
		return menu is null ? Problem(statusCode: 404, title: "Menu not found.") : Ok(menu);
	}
}
