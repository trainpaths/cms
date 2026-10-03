namespace api_backend.Models.Auth;

public enum VerificationTokenPurpose
{
	EmailVerification = 0,
	PasswordReset = 1,
}

/// <summary>
/// A single-purpose, hashed token backing the email-verification and password-reset flows.
/// Mirrors <see cref="JWT.RefreshToken"/>: only the SHA-256 hash is stored at rest (the raw token
/// lives only in the emailed link) and the token belongs to exactly one principal — a
/// <see cref="Customer"/> or a <see cref="Staff"/> — enforced by a DB check constraint.
/// </summary>
public class VerificationToken
{
	public Guid Id { get; set; } = Guid.NewGuid();

	/// <summary>SHA-256 hash of the raw token; lookups hash the incoming token.</summary>
	public required string TokenHash { get; set; }

	public VerificationTokenPurpose Purpose { get; set; }

	public Guid? CustomerId { get; set; }
	public Customer? Customer { get; set; }

	public Guid? StaffId { get; set; }
	public Staff? Staff { get; set; }

	public DateTimeOffset ExpiresAt { get; set; }
	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>Set when the token is redeemed; a consumed token can never be reused.</summary>
	public DateTimeOffset? ConsumedAt { get; set; }

	public bool IsActive => ConsumedAt is null && DateTimeOffset.UtcNow < ExpiresAt;
}
