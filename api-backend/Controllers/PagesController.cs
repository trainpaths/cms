using api_backend.Models.Auth.JWT;
using api_backend.Models.Dto;
using api_backend.Models.Pages;
using api_backend.Services.Pages;
using api_backend.Services.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace api_backend.Controllers;

/// <summary>Page management for the block editor. Staff (the site owners) only.</summary>
[ApiController]
[Route("api/pages")]
[Authorize(Policy = AuthPolicies.StaffOnly)]
[EnableRateLimiting(RateLimitPolicies.Pages)]
public class PagesController(PageService pages, IRenderQueue render) : ControllerBase
{
	/// <summary>Block trees can outgrow the global 64 KB body limit.</summary>
	public const long MaxPageBodyBytes = 2 * 1024 * 1024;

	private Guid CurrentUserId => User.GetUserId();

	[HttpGet]
	public async Task<ActionResult<List<PageSummary>>> List(CancellationToken ct) =>
		Ok(await pages.ListAsync(ct));

	[HttpGet("{id:guid}")]
	public async Task<ActionResult<PageDetail>> Get(Guid id, CancellationToken ct)
	{
		var page = await pages.GetAsync(id, ct);
		return page is null ? NotFoundProblem() : Ok(page);
	}

	[HttpPost]
	[RequestSizeLimit(MaxPageBodyBytes)]
	public async Task<ActionResult<PageDetail>> Create(CreatePageRequest req, CancellationToken ct)
	{
		var result = await pages.CreateAsync(req, CurrentUserId, ct);
		return result.Error is null
			? CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value)
			: ToProblem(result);
	}

	[HttpPut("{id:guid}")]
	[RequestSizeLimit(MaxPageBodyBytes)]
	public async Task<ActionResult<PageDetail>> Update(Guid id, UpdatePageRequest req, CancellationToken ct)
	{
		var result = await pages.UpdateAsync(id, req, CurrentUserId, ct);
		// no draft copy: edits to a published page are live (worker widens to all on title/slug change)
		if (result.Value?.Status == PageStatus.Published) render.Page(id);
		return Map(result);
	}

	[HttpPut("{id:guid}/tags")]
	public async Task<ActionResult<PageDetail>> SetTags(Guid id, UpdatePageTagsRequest req, CancellationToken ct) =>
		Map(await pages.SetTagsAsync(id, req.Tags, ct));

	[HttpPost("{id:guid}/publish")]
	public Task<ActionResult<PageDetail>> Publish(Guid id, CancellationToken ct) =>
		SetStatusAsync(id, PageStatus.Published, ct);

	[HttpPost("{id:guid}/unpublish")]
	public Task<ActionResult<PageDetail>> Unpublish(Guid id, CancellationToken ct) =>
		SetStatusAsync(id, PageStatus.Draft, ct);

	[HttpDelete("{id:guid}")]
	public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
	{
		var result = await pages.DeleteAsync(id, ct);
		if (result.Error is not null)
			return ToProblem(result);
		render.All();
		return NoContent();
	}

	// menus + footer links on every page resolve published pages only
	private async Task<ActionResult<PageDetail>> SetStatusAsync(Guid id, PageStatus status, CancellationToken ct)
	{
		var result = await pages.SetStatusAsync(id, status, CurrentUserId, ct);
		if (result.Error is null) render.All();
		return Map(result);
	}

	private ActionResult<PageDetail> Map(PageResult<PageDetail> result) =>
		result.Error is null ? Ok(result.Value) : ToProblem(result);

	private ObjectResult ToProblem<T>(PageResult<T> result) => result.Error switch
	{
		PageError.NotFound => NotFoundProblem(),
		PageError.SlugTaken => Problem(statusCode: 409, title: "Slug already in use.", detail: result.Detail),
		_ => Problem(statusCode: 400, title: "Invalid page.", detail: result.Detail),
	};

	private ObjectResult NotFoundProblem() => Problem(statusCode: 404, title: "Page not found.");
}
