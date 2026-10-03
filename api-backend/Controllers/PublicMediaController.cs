using api_backend.Models.Auth.JWT;
using api_backend.Services.Media;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace api_backend.Controllers;

/// <summary>
/// Anonymous reads of uploaded files, streamed from blob storage. The store itself is never exposed;
/// keys are unguessable and immutable (new upload = new key), so responses are cached for a year.
/// </summary>
[ApiController]
[Route("api/public/media")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Pages)]
public class PublicMediaController(MediaService media) : ControllerBase
{
	[HttpGet("{key}")]
	[ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK, "image/jpeg", "image/png", "image/gif", "image/webp", "image/avif")]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Get(string key, CancellationToken ct)
	{
		var blob = await media.OpenPublicAsync(key, ct);
		if (blob is null)
			return Problem(statusCode: 404, title: "Media not found.");

		Response.RegisterForDisposeAsync(blob);
		Response.Headers.CacheControl = "public, max-age=31536000, immutable";
		// Defense in depth for user uploads served from the app origin
		Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
		Response.Headers.ContentDisposition = "inline";
		return File(blob.Content, blob.ContentType);
	}
}
