using api_backend.Models.Auth.JWT;
using api_backend.Models.Dto;
using api_backend.Services.Backup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace api_backend.Controllers;

/// <summary>Full site backups (database + media) and their schedule. Super admins only: a restore replaces everything.</summary>
[ApiController]
[Route("api/backups")]
[Authorize(Policy = AuthPolicies.SuperAdmin)]
[EnableRateLimiting(RateLimitPolicies.General)]
public class BackupsController(BackupService backups) : ControllerBase
{
	[HttpGet("settings")]
	public async Task<ActionResult<BackupSettingsResponse>> GetSettings(CancellationToken ct) =>
		Ok(await backups.GetSettingsAsync(ct));

	[HttpPut("settings")]
	public async Task<ActionResult<BackupSettingsResponse>> UpdateSettings(UpdateBackupSettingsRequest req, CancellationToken ct) =>
		Ok(await backups.UpdateSettingsAsync(req, ct));

	/// <summary>Archives in the backup directory, newest first.</summary>
	[HttpGet]
	public async Task<ActionResult<List<BackupInfo>>> List(CancellationToken ct) =>
		Ok(await backups.ListAsync(ct));

	/// <summary>Back up now (kind <c>manual</c>).</summary>
	[HttpPost]
	public Task<ActionResult<BackupInfo>> Create(CancellationToken ct) =>
		RunAsync(async () => Map(await backups.CreateAsync(BackupKind.Manual, ct), created: true));

	[HttpGet("{name}")]
	[ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK, "application/gzip")]
	public IActionResult Download(string name) =>
		backups.PathOf(name) is { } path
			? PhysicalFile(path, "application/gzip", name, enableRangeProcessing: true)
			: NotFoundProblem();

	[HttpDelete("{name}")]
	public IActionResult Delete(string name)
	{
		var result = backups.Delete(name);
		return result.Error is null ? NoContent() : ToProblem(result);
	}

	/// <summary>Adds an archive from elsewhere (stored as <c>upload-…</c>); restore it afterwards.</summary>
	[HttpPost("upload")]
	[Consumes("multipart/form-data")]
	[DisableRequestSizeLimit]
	[RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
	public Task<ActionResult<BackupInfo>> Upload(IFormFile file, CancellationToken ct) =>
		RunAsync(async () =>
		{
			if (file.Length == 0 || file.Length > backups.MaxUploadBytes)
				return Problem(statusCode: 400, title: "Invalid backup.",
					detail: $"The file must be 1 byte to {backups.MaxUploadBytes / 1024 / 1024} MB.");
			await using var stream = file.OpenReadStream();
			return Map(await backups.SaveUploadAsync(stream, ct), created: true);
		});

	/// <summary>
	/// Replaces the whole site (pages, media, menus, config, staff accounts) with the backup. Takes a
	/// <c>pre-restore</c> backup first; every session ends (sign in again).
	/// </summary>
	[HttpPost("{name}/restore")]
	public Task<ActionResult<RestoreResult>> Restore(string name, CancellationToken ct) =>
		RunAsync<RestoreResult>(async () =>
		{
			var result = await backups.RestoreAsync(name, ct);
			return result.Error is null ? Ok(new RestoreResult(result.Value!)) : ToProblem(result);
		});

	// tool and disk failures carry a message worth showing the super admin (the global handler hides details)
	private async Task<ActionResult<T>> RunAsync<T>(Func<Task<ActionResult<T>>> action)
	{
		try
		{
			return await action();
		}
		catch (BackupFailedException e)
		{
			return Problem(statusCode: 500, title: "Backup operation failed.", detail: e.Message);
		}
	}

	private ActionResult<BackupInfo> Map(BackupResult<BackupInfo> result, bool created) =>
		result.Error is not null ? ToProblem(result)
		: created ? StatusCode(StatusCodes.Status201Created, result.Value)
		: Ok(result.Value);

	private ObjectResult ToProblem<T>(BackupResult<T> result) => result.Error switch
	{
		BackupError.NotFound => NotFoundProblem(),
		BackupError.Busy => Problem(statusCode: 409, title: "Backup busy.", detail: result.Detail),
		_ => Problem(statusCode: 400, title: "Invalid backup.", detail: result.Detail),
	};

	private ObjectResult NotFoundProblem() => Problem(statusCode: 404, title: "Backup not found.");
}
