namespace api_backend.Models.Auth;

/// <summary>Members shared by <see cref="Customer"/> and <see cref="Staff"/> for the common auth flows.</summary>
public interface IAuthPrincipal
{
	Guid Id { get; }
	string Email { get; }
	string PasswordHash { get; set; }
	string? DisplayName { get; set; }
	bool IsActive { get; }
	DateTimeOffset? UpdatedAt { get; set; }
	DateTimeOffset? LastLoginAt { get; set; }
}
