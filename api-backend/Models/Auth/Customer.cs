using api_backend.Models.Auth.JWT;

namespace api_backend.Models.Auth;

/// <summary>
/// A regular end-user of the website (the "customer" who logs in to use the public app).
/// Kept in its own table, fully independent from <see cref="Staff"/>.
/// </summary>
public class Customer : IAuthPrincipal
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public required string Email { get; set; }
	public required string PasswordHash { get; set; }

	public string? DisplayName { get; set; }

	public bool IsEmailConfirmed { get; set; }
	public bool IsActive { get; set; } = true;

	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
	public DateTimeOffset? UpdatedAt { get; set; }
	public DateTimeOffset? LastLoginAt { get; set; }

	public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
	public ICollection<VerificationToken> VerificationTokens { get; set; } = new List<VerificationToken>();
}