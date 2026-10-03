using api_backend.Models.Auth;
using api_backend.Services.Auth.JWT;
using Microsoft.EntityFrameworkCore;

namespace api_backend.Services.Auth;

/// <summary>Creates the first super admin from <c>Bootstrap:*</c> settings; no-op once one exists.</summary>
public static class StaffBootstrapper
{
	public const string SuperAdminRole = "super_admin";
	private const int MinPasswordLength = 12;

	public static async Task EnsureSuperAdminAsync(IServiceProvider services, CancellationToken ct = default)
	{
		using var scope = services.CreateScope();
		var sp = scope.ServiceProvider;
		var config = sp.GetRequiredService<IConfiguration>();

		var email = config["Bootstrap:SuperAdminEmail"]?.Trim().ToLowerInvariant();
		var password = config["Bootstrap:SuperAdminPassword"];
		if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password)) return;

		var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(StaffBootstrapper));
		if (password.Length < MinPasswordLength)
		{
			logger.LogError(
				"Bootstrap:SuperAdminPassword must be at least {MinLength} characters; no super admin created.",
				MinPasswordLength);
			return;
		}

		var db = sp.GetRequiredService<AppDbContext>();
		if (await db.StaffRoles.AnyAsync(sr => sr.Role.Name == SuperAdminRole, ct)) return;

		if (await db.Staff.AnyAsync(s => s.Email == email, ct))
		{
			logger.LogWarning(
				"Bootstrap super admin {Email} already exists without the {Role} role; not modifying it.",
				email, SuperAdminRole);
			return;
		}

		var role = await db.Roles.FirstAsync(r => r.Name == SuperAdminRole, ct);
		var hasher = sp.GetRequiredService<PasswordHashService>();
		var staff = new Staff
		{
			Email = email,
			PasswordHash = hasher.Hash(password),
			DisplayName = "Super Admin",
		};
		staff.StaffRoles.Add(new StaffRole { Staff = staff, Role = role });
		db.Staff.Add(staff);
		await db.SaveChangesAsync(ct);

		logger.LogInformation("Created bootstrap super admin {Email}.", email);
	}
}
