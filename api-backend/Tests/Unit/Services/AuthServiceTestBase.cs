using api_backend.Models.Auth.JWT;
using api_backend.Services.Auth;
using api_backend.Services.Auth.JWT;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace api_backend.Tests.Unit.Services;

// Refresh rotation uses ExecuteUpdate, which InMemory doesn't support; it's covered by integration tests.
public abstract class AuthServiceTestBase
{
	protected readonly PasswordHashService Hasher = new();
	protected readonly JwtTokenService Tokens = new(Options.Create(new JwtOptions
	{
		Issuer = "test",
		Audience = "test",
		SigningKey = "test-signing-key-must-be-at-least-32-chars",
		AccessTokenMinutes = 15,
		RefreshTokenDays = 30,
	}));

	protected static CancellationToken Ct => TestContext.Current.CancellationToken;

	protected RefreshTokenService RefreshTokens(AppDbContext db) =>
		new(db, Tokens, NullLogger<RefreshTokenService>.Instance);
}
