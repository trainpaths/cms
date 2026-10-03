using api_backend.Models.Auth.JWT;
using api_backend.Models.Dto;
using api_backend.Services.Auth;
using Microsoft.AspNetCore.Mvc;

namespace api_backend.Controllers;

public abstract class AuthControllerBase : ControllerBase
{
	public const string RefreshCookieName = "refresh_token";

	protected abstract string CookiePath { get; }

	protected string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

	protected Guid CurrentUserId => User.GetUserId();

	protected string? RefreshCookie => Request.Cookies[RefreshCookieName];

	protected ActionResult<AuthResponse> StartSession(AuthResult result)
	{
		Response.Cookies.Append(RefreshCookieName, result.Refresh.RawToken, BuildCookieOptions(result.Refresh.ExpiresAt));
		return Ok(result.Response);
	}

	protected void EndSession() => Response.Cookies.Delete(RefreshCookieName, BuildCookieOptions(expires: null));

	private CookieOptions BuildCookieOptions(DateTimeOffset? expires)
	{
		var services = HttpContext.RequestServices;
		var secure = services.GetRequiredService<IConfiguration>()
			.GetValue("Auth:SecureCookie", !services.GetRequiredService<IHostEnvironment>().IsDevelopment());

		return new CookieOptions
		{
			HttpOnly = true,
			Secure = secure,
			SameSite = SameSiteMode.Strict,
			Path = CookiePath,
			Expires = expires,
			IsEssential = true,
		};
	}
}
