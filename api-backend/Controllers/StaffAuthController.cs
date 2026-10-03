using api_backend.Models.Auth.JWT;
using api_backend.Models.Dto;
using api_backend.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace api_backend.Controllers;

[ApiController]
[Route("api/auth/staff")]
[EnableRateLimiting(RateLimitPolicies.General)]
public class StaffAuthController(StaffAuthService auth) : AuthControllerBase
{
	protected override string CookiePath => "/api/auth/staff";

	// No open staff signup; drop attribute to allow self-service.
	[Authorize(Policy = AuthPolicies.SuperAdmin)]
	[HttpPost("register")]
	public async Task<ActionResult<UserInfo>> Register(StaffRegisterRequest req, CancellationToken ct)
	{
		var result = await auth.RegisterAsync(req, ct);
		return result is null
			? Problem(statusCode: StatusCodes.Status409Conflict, title: "Email already in use.")
			: Ok(result);
	}

	[HttpPost("login")]
	[EnableRateLimiting(RateLimitPolicies.Credentials)]
	public async Task<ActionResult<AuthResponse>> Login(LoginRequest req, CancellationToken ct)
	{
		var result = await auth.LoginAsync(req, ClientIp, ct);
		return result is null
			? Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials.")
			: StartSession(result);
	}

	[HttpPost("refresh")]
	public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct)
	{
		var result = await auth.RefreshAsync(RefreshCookie, ClientIp, ct);
		return result is null
			? Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid or expired refresh token.")
			: StartSession(result);
	}

	[HttpPost("logout")]
	public async Task<IActionResult> Logout(CancellationToken ct)
	{
		await auth.LogoutAsync(RefreshCookie, ct);
		EndSession();
		return NoContent();
	}

	[Authorize(Policy = AuthPolicies.StaffOnly)]
	[HttpGet("me")]
	public async Task<ActionResult<UserInfo>> Me(CancellationToken ct)
	{
		var result = await auth.GetMeAsync(CurrentUserId, ct);
		return result is null ? NotFound() : Ok(result);
	}

	[Authorize(Policy = AuthPolicies.StaffOnly)]
	[HttpPost("change-password")]
	[EnableRateLimiting(RateLimitPolicies.Credentials)]
	public async Task<IActionResult> ChangePassword(ChangePasswordRequest req, CancellationToken ct)
	{
		var success = await auth.ChangePasswordAsync(CurrentUserId, req.CurrentPassword, req.NewPassword, ct);
		if (!success)
			return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Current password is incorrect.");

		EndSession();
		return NoContent();
	}

	[Authorize(Policy = AuthPolicies.StaffOnly)]
	[HttpPost("profile")]
	public async Task<ActionResult<UserInfo>> UpdateProfile(UpdateProfileRequest req, CancellationToken ct)
	{
		var result = await auth.UpdateProfileAsync(CurrentUserId, req.DisplayName, ct);
		return result is null ? NotFound() : Ok(result);
	}
}
