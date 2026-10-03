using api_backend.Models.Auth.JWT;
using api_backend.Models.Dto;
using api_backend.Services.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace api_backend.Controllers;

/// <summary>Page tags in use. Tags are set per page (<c>PUT /api/pages/{id}/tags</c>).</summary>
[ApiController]
[Route("api/tags")]
[Authorize(Policy = AuthPolicies.StaffOnly)]
[EnableRateLimiting(RateLimitPolicies.Pages)]
public class TagsController(PageService pages) : ControllerBase
{
	[HttpGet]
	public async Task<ActionResult<List<TagUsage>>> List(CancellationToken ct) =>
		Ok(await pages.ListTagsAsync(ct));
}
