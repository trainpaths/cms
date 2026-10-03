using System.Security.Claims;

namespace api_backend.Models.Auth.JWT;

public static class ClaimsPrincipalExtensions
{
	public static Guid GetUserId(this ClaimsPrincipal user) =>
		Guid.Parse(user.FindFirst("sub")?.Value
			?? throw new InvalidOperationException("Authenticated principal has no 'sub' claim."));
}
