namespace api_backend.Models.Auth.JWT;

public enum UserType
{
	Customer = 0,
	Staff = 1,
}

/// <summary>
/// A persisted refresh token enabling rotation and revocation. A token belongs to
/// exactly one principal — either a <see cref="Customer"/> or a <see cref="StaffUser"/> —
/// distinguished by <see cref="UserType"/>. Only the matching FK is populated.
/// </summary>
public class RefreshToken
{
	public Guid Id { get; set; } = Guid.NewGuid();

	/// <summary>Opaque, cryptographically-random token string (hashed at rest below).</summary>
	public required string TokenHash { get; set; }

	public UserType UserType { get; set; }

	public Guid? CustomerId { get; set; }
	public Customer? Customer { get; set; }

	public Guid? StaffId { get; set; }
	public Staff? Staff { get; set; }

	public DateTimeOffset ExpiresAt { get; set; }
	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

	public DateTimeOffset? RevokedAt { get; set; }
	/// <summary>Id of the token that replaced this one (rotation chain).</summary>
	public Guid? ReplacedByTokenId { get; set; }

	public string? CreatedByIp { get; set; }

	public bool IsActive => RevokedAt is null && DateTimeOffset.UtcNow < ExpiresAt;
}