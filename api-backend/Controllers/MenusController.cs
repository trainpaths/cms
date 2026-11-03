using api_backend.Models.Auth.JWT;
using api_backend.Models.Dto;
using api_backend.Models.Menus;
using api_backend.Services;
using api_backend.Services.Menus;
using api_backend.Services.Pages;
using api_backend.Services.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace api_backend.Controllers;

/// <summary>Menu management (main navigation + any extra menus). Staff only.</summary>
[ApiController]
[Route("api/menus")]
[Authorize(Policy = AuthPolicies.StaffOnly)]
[EnableRateLimiting(RateLimitPolicies.Pages)]
public class MenusController(MenuService menus, IRenderQueue render, PublicCache cache) : ControllerBase
{
	/// <summary>200 items with long URLs can outgrow the global 64 KB body limit.</summary>
	public const long MaxMenuBodyBytes = 512 * 1024;

	[HttpGet]
	public async Task<ActionResult<List<MenuDetail>>> List(CancellationToken ct) =>
		Ok(await menus.ListAsync(ct));

	[HttpGet("{id:guid}")]
	public async Task<ActionResult<MenuDetail>> Get(Guid id, CancellationToken ct)
	{
		var menu = await menus.GetAsync(id, ct);
		return menu is null ? NotFoundProblem() : Ok(menu);
	}

	[HttpPost]
	public async Task<ActionResult<MenuDetail>> Create(CreateMenuRequest req, CancellationToken ct)
	{
		var result = await menus.CreateAsync(req, User.GetUserId(), ct);
		if (result.Error is not null)
			return ToProblem(result);
		cache.Invalidate();
		return CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value);
	}

	[HttpPut("{id:guid}")]
	[RequestSizeLimit(MaxMenuBodyBytes)]
	public async Task<ActionResult<MenuDetail>> Update(Guid id, UpdateMenuRequest req, CancellationToken ct)
	{
		var result = await menus.UpdateAsync(id, req, User.GetUserId(), ct);
		if (result.Error is not null)
			return ToProblem(result);
		// only main is in the rendered state; other menus exist only client-side (Refresh() invalidates too)
		if (result.Value!.Handle == Menu.MainHandle) render.Refresh();
		else cache.Invalidate();
		return Ok(result.Value);
	}

	[HttpDelete("{id:guid}")]
	public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
	{
		var result = await menus.DeleteAsync(id, ct);
		if (result.Error is not null)
			return ToProblem(result);
		// nothing to render: the main menu can't be deleted, other menus aren't in stored HTML
		cache.Invalidate();
		return NoContent();
	}

	private static readonly ProblemTitles Titles = new("Menu not found.", "Invalid menu.", "Handle already in use.");

	private ObjectResult ToProblem<T>(ServiceResult<T> result) => this.ToProblem(result, Titles);

	private ObjectResult NotFoundProblem() => this.NotFoundProblem(Titles);
}
