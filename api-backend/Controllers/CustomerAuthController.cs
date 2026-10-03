using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using api_backend.Services.Auth;
using api_backend.Models.Dto;
using api_backend.Models.Auth.JWT;
using api_backend.Services.Cms;

namespace api_backend.Controllers;

[ApiController]
[Route("api/auth/customer")]
[EnableRateLimiting(RateLimitPolicies.General)]
[PublicAuthGate]
public class CustomerAuthController(CustomerAuthService auth, AccountService account) : AuthControllerBase
{
	protected override string CookiePath => "/api/auth/customer";

	[HttpPost("register")]
	[EnableRateLimiting(RateLimitPolicies.Credentials)]
	public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req, CancellationToken ct)
	{
		var result = await auth.RegisterAsync(req, ClientIp, ct);
		return result is null
			? Problem(statusCode: StatusCodes.Status409Conflict, title: "Email already in use.")
			: StartSession(result);
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
		// Keep the cookie on failure: another tab may have just rotated it.
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

	// Email verification + password reset. "request" endpoints never reveal if account exists.

	[HttpPost("request-verification")]
	[EnableRateLimiting(RateLimitPolicies.Credentials)]
	public async Task<IActionResult> RequestVerification(RequestVerificationRequest req, CancellationToken ct)
	{
		await account.RequestVerificationAsync(req.Email, ct);
		return NoContent();
	}

	[HttpPost("confirm-email")]
	[EnableRateLimiting(RateLimitPolicies.Credentials)]
	public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest req, CancellationToken ct)
	{
		var success = await account.ConfirmEmailAsync(req.Token, ct);
		return success
			? NoContent()
			: Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid or expired token.");
	}

	[HttpPost("forgot-password")]
	[EnableRateLimiting(RateLimitPolicies.Credentials)]
	public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest req, CancellationToken ct)
	{
		await account.ForgotPasswordAsync(req.Email, ct);
		return NoContent();
	}

	[HttpPost("reset-password")]
	[EnableRateLimiting(RateLimitPolicies.Credentials)]
	public async Task<IActionResult> ResetPassword(ResetPasswordRequest req, CancellationToken ct)
	{
		var success = await account.ResetPasswordAsync(req.Token, req.NewPassword, ct);
		return success
			? NoContent()
			: Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid or expired token.");
	}

	[Authorize(Policy = AuthPolicies.CustomerOnly)]
	[HttpGet("me")]
	public async Task<ActionResult<UserInfo>> Me(CancellationToken ct)
	{
		var result = await auth.GetMeAsync(CurrentUserId, ct);
		return result is null ? NotFound() : Ok(result);
	}

	[Authorize(Policy = AuthPolicies.CustomerOnly)]
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

	[Authorize(Policy = AuthPolicies.CustomerOnly)]
	[HttpPost("profile")]
	public async Task<ActionResult<UserInfo>> UpdateProfile(UpdateProfileRequest req, CancellationToken ct)
	{
		var result = await auth.UpdateProfileAsync(CurrentUserId, req.DisplayName, ct);
		return result is null ? NotFound() : Ok(result);
	}
}
