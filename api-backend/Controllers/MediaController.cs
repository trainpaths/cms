using api_backend.Models.Auth.JWT;
using api_backend.Models.Dto;
using api_backend.Services.Media;
using api_backend.Services.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace api_backend.Controllers;

/// <summary>Media library for the block editor. Staff (the site owners) only.</summary>
[ApiController]
[Route("api/media")]
[Authorize(Policy = AuthPolicies.StaffOnly)]
[EnableRateLimiting(RateLimitPolicies.Pages)]
public class MediaController(MediaService media, IRenderQueue render) : ControllerBase
{
	// Headroom for multipart boundaries + the alt field on top of the file limit.
	private const long MaxRequestBytes = MediaService.MaxUploadBytes + 64 * 1024;

	[HttpGet]
	public async Task<ActionResult<List<MediaItem>>> List(CancellationToken ct) =>
		Ok(await media.ListAsync(ct));

	[HttpGet("{id:guid}")]
	public async Task<ActionResult<MediaItem>> Get(Guid id, CancellationToken ct)
	{
		var item = await media.GetAsync(id, ct);
		return item is null ? NotFoundProblem() : Ok(item);
	}

	/// <summary>Upload an image (JPEG, PNG, GIF, WebP, AVIF; max 10 MB).</summary>
	[HttpPost]
	[Consumes("multipart/form-data")]
	[RequestSizeLimit(MaxRequestBytes)]
	[RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
	public async Task<ActionResult<MediaItem>> Upload(IFormFile file, [FromForm] string? alt, CancellationToken ct)
	{
		var result = await media.UploadAsync(file, alt, User.GetUserId(), ct);
		return result.Error is null
			? CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value)
			: ToProblem(result);
	}

	[HttpPut("{id:guid}")]
	public async Task<ActionResult<MediaItem>> Update(Guid id, UpdateMediaRequest req, CancellationToken ct)
	{
		var result = await media.UpdateAsync(id, req, ct);
		if (result.Error is not null)
			return ToProblem(result);
		render.All(); // alt text is in rendered pages (no usage tracking to narrow it)
		return Ok(result.Value);
	}

	/// <summary>Blocks still referencing the asset render nothing afterwards.</summary>
	[HttpDelete("{id:guid}")]
	public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
	{
		if (!await media.DeleteAsync(id, ct))
			return NotFoundProblem();
		render.All();
		return NoContent();
	}

	private ObjectResult ToProblem<T>(MediaResult<T> result) => result.Error switch
	{
		MediaError.NotFound => NotFoundProblem(),
		_ => Problem(statusCode: 400, title: "Invalid media.", detail: result.Detail),
	};

	private ObjectResult NotFoundProblem() => Problem(statusCode: 404, title: "Media not found.");
}
