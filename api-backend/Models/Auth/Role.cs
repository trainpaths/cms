namespace api_backend.Models.Auth;

/// <summary>
/// A named role assignable to staff users. Seeded with a couple of defaults but
/// designed to be extended per concrete website.
/// </summary>
public class Role
{
	public Guid Id { get; set; } = Guid.NewGuid();

	/// <summary>Stable machine name, e.g. "super_admin", "restaurant_owner".</summary>
	public required string Name { get; set; }

	public string? Description { get; set; }

	public ICollection<StaffRole> StaffRoles { get; set; } = new List<StaffRole>();
}

/// <summary>Many-to-many join between <see cref="StaffUser"/> and <see cref="Role"/>.</summary>
public class StaffRole
{
	public Guid StaffId { get; set; }
	public Staff Staff { get; set; } = null!;

	public Guid RoleId { get; set; }
	public Role Role { get; set; } = null!;

	public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
}