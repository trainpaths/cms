using api_backend.Models.Auth.JWT;

namespace api_backend.Models.Auth;

/// <summary>
/// An internal user (admin / staff). Lives in its own table, separate from
/// <see cref="Customer"/>. Has DB-backed, runtime-assignable roles via
/// <see cref="StaffRole"/> so a template consumer can define whatever roles
/// the concrete website needs (e.g. "RestaurantOwner", "KitchenStaff", "SuperAdmin").
/// </summary>
public class Staff : IAuthPrincipal
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public required string Email { get; set; }
	public required string PasswordHash { get; set; }

	public string? DisplayName { get; set; }

	/// <summary>
	/// Optional free-form tenant/organisation key. Lets the same staff table back a
	/// multi-tenant template ("this staff user belongs to restaurant X") without
	/// forcing a tenant model on consumers who don't need one.
	/// </summary>
	public string? OrganizationId { get; set; }

	public bool IsActive { get; set; } = true;

	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
	public DateTimeOffset? UpdatedAt { get; set; }
	public DateTimeOffset? LastLoginAt { get; set; }

	public ICollection<StaffRole> StaffRoles { get; set; } = new List<StaffRole>();
	public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}