namespace api_backend.Models.Auth.JWT;

/// <summary>Bound from the "Jwt" configuration section (env-overridable via Jwt__*).</summary>
public class JwtOptions
{
	public const string SectionName = "Jwt";

	public string Issuer { get; set; } = "website-template";
	public string Audience { get; set; } = "website-clients";

	/// <summary>HMAC signing key. MUST be overridden via env in any real deployment.</summary>
	public string SigningKey { get; set; } = string.Empty;

	public int AccessTokenMinutes { get; set; } = 15;
	public int RefreshTokenDays { get; set; } = 30;
}
