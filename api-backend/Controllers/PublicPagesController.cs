using api_backend.Models.Auth.JWT;
using api_backend.Models.Dto;
using api_backend.Services.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace api_backend.Controllers;

/// <summary>Anonymous read access to published pages (rendered by the frontend at <c>/:slug</c>).</summary>
[ApiController]
[Route("api/public/pages")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Pages)]
public class PublicPagesController(PageService pages) : ControllerBase
{
	[HttpGet("{slug}")]
	public async Task<ActionResult<PublicPage>> GetBySlug(string slug, CancellationToken ct)
	{
		var page = await pages.GetPublishedBySlugAsync(slug, ct);
		return page is null ? Problem(statusCode: 404, title: "Page not found.") : Ok(page);
	}
}
