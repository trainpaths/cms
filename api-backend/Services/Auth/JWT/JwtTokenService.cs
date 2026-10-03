using System.Security.Claims;
using System.Text;
using api_backend.Models.Auth.JWT;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace api_backend.Services.Auth.JWT;

public record AccessToken(string Token, DateTimeOffset ExpiresAt);
public record GeneratedRefreshToken(string RawToken, string TokenHash, DateTimeOffset ExpiresAt);

public class JwtTokenService
{
	private static readonly JsonWebTokenHandler Handler = new();

	private readonly JwtOptions _o;
	private readonly SigningCredentials _credentials;

	public JwtTokenService(IOptions<JwtOptions> options)
	{
		_o = options.Value;
		_credentials = new SigningCredentials(
			new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_o.SigningKey)),
			SecurityAlgorithms.HmacSha256);
	}

	public AccessToken CreateAccessToken(
		Guid userId,
		string email,
		string userType,
		IEnumerable<string> roles,
		string? organizationId = null)
	{
		var now = DateTime.UtcNow;
		var expires = now.AddMinutes(_o.AccessTokenMinutes);

		var claims = new List<Claim>
		{
			new(JwtRegisteredClaimNames.Sub, userId.ToString()),
			new(JwtRegisteredClaimNames.Email, email),
			new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
			new(AuthClaims.UserType, userType),
		};

		if (!string.IsNullOrWhiteSpace(organizationId))
			claims.Add(new Claim(AuthClaims.OrganizationId, organizationId));

		claims.AddRange(roles.Select(r => new Claim("role", r)));

		var token = Handler.CreateToken(new SecurityTokenDescriptor
		{
			Issuer = _o.Issuer,
			Audience = _o.Audience,
			Subject = new ClaimsIdentity(claims),
			NotBefore = now,
			IssuedAt = now,
			Expires = expires,
			SigningCredentials = _credentials,
		});

		return new AccessToken(token, new DateTimeOffset(expires, TimeSpan.Zero));
	}

	public GeneratedRefreshToken CreateRefreshToken()
	{
		var generated = SecureToken.Create();
		return new GeneratedRefreshToken(
			generated.RawToken,
			generated.TokenHash,
			DateTimeOffset.UtcNow.AddDays(_o.RefreshTokenDays));
	}

	/// <summary>SHA-256 so only hashes are stored at rest; lookups hash the incoming token.</summary>
	public string HashRefreshToken(string rawToken) => SecureToken.Hash(rawToken);
}
