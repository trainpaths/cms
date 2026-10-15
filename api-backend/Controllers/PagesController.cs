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

	[HttpPost("import")]
	[RequestSizeLimit(MaxPageBodyBytes)]
	public async Task<ActionResult<ImportPageResult>> Import(ImportPageRequest req, CancellationToken ct)
	{
		var result = await pages.ImportAsync(req, CurrentUserId, ct);
		return result.Error is null
			? CreatedAtAction(nameof(Get), new { id = result.Value!.Page.Id }, result.Value)
			: ToProblem(result);
	}

	/// <summary>
	/// Partial update of content, meta, slug, status and tags in one call. Content edits the working copy; status
	/// <c>published</c> publishes it. Returns the slim summary: the editor already has the blocks it sent (up to 2 MB, not
	/// echoed back).
	/// </summary>
	[HttpPut("{id:guid}")]
	[RequestSizeLimit(MaxPageBodyBytes)]
	public async Task<ActionResult<PageSummary>> Update(Guid id, UpdatePageRequest req, CancellationToken ct)
	{
		var result = await pages.UpdateAsync(id, req, CurrentUserId, ct);
		if (result.Error is not null)
			return ToProblem(result);
		var (page, statusChanged, liveChanged, liveRenamed) = result.Value!;
		if (statusChanged)
		{
			// menus + footer links on every page resolve published pages only: re-render all quietly, other pages
			// marked right away. Page(id) too: HTML stored before an unpublish must not come back on a republish.
			render.Page(id);
			render.Refresh();
		}
		else if (liveChanged)
		{
			// published changes / new slug; saves of the working copy render nothing. A new live title/slug shows in
			// menus + footer links on every page: re-render all quietly, other pages marked right away.
			render.Page(id);
			if (liveRenamed) render.Refresh();
		}
		return Ok(page);
	}

	/// <summary>Draft copy of the page (blocks, meta, tags) without the blocks round trip through the browser.</summary>
	[HttpPost("{id:guid}/duplicate")]
	public async Task<ActionResult<PageDetail>> Duplicate(Guid id, CancellationToken ct)
	{
		var result = await pages.DuplicateAsync(id, CurrentUserId, ct);
		return result.Error is null
			? CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value)
			: ToProblem(result);
	}

	[HttpDelete("{id:guid}")]
	public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
	{
		var result = await pages.DeleteAsync(id, ct);
		if (result.Error is not null)
			return ToProblem(result);
		// the page left the public site: re-render all quietly, other pages marked right away
		render.Refresh();
		return NoContent();
	}

	private ObjectResult ToProblem<T>(PageResult<T> result) => result.Error switch
	{
		PageError.NotFound => NotFoundProblem(),
		PageError.SlugTaken => Problem(statusCode: 409, title: "Slug already in use.", detail: result.Detail),
		_ => Problem(statusCode: 400, title: "Invalid page.", detail: result.Detail),
	};

	private ObjectResult NotFoundProblem() => Problem(statusCode: 404, title: "Page not found.");
}
