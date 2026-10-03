using api_backend.Models.Auth;
using api_backend.Models.Auth.JWT;
using api_backend.Models.Dto;
using api_backend.Services.Auth.JWT;
using Microsoft.EntityFrameworkCore;

namespace api_backend.Services.Auth;

public class StaffAuthService(
	AppDbContext db,
	PasswordHashService hasher,
	JwtTokenService tokens,
	RefreshTokenService refreshTokens)
	: PrincipalAuthService<Staff>(db, hasher, tokens, refreshTokens)
{
	public const string DefaultRole = "staff";

	protected override UserType Type => UserType.Staff;

	protected override IQueryable<Staff> Users =>
		Db.Staff.Include(s => s.StaffRoles).ThenInclude(sr => sr.Role);

	protected override UserInfo ToUserInfo(Staff staff) =>
		new(staff.Id, staff.Email, staff.DisplayName, AuthClaims.StaffUserType, RoleNames(staff), staff.OrganizationId);

	protected override AccessToken CreateAccessToken(Staff staff) =>
		Tokens.CreateAccessToken(staff.Id, staff.Email, AuthClaims.StaffUserType, RoleNames(staff), staff.OrganizationId);

	private static string[] RoleNames(Staff staff) => staff.StaffRoles.Select(sr => sr.Role.Name).ToArray();

	// Called by an admin, so the new account is not signed in.
	public async Task<UserInfo?> RegisterAsync(StaffRegisterRequest req, CancellationToken ct = default)
	{
		var email = NormalizeEmail(req.Email);
		if (await Db.Staff.AnyAsync(s => s.Email == email, ct))
			return null;

		var staff = new Staff
		{
			Email = email,
			PasswordHash = Hasher.Hash(req.Password),
			DisplayName = req.DisplayName,
			OrganizationId = req.OrganizationId,
		};

				var requested = (req.Roles is { Length: > 0 } ? req.Roles : [DefaultRole])
			.Select(r => r.Trim().ToLowerInvariant())
			.Distinct()
			.ToArray();

		var roles = await Db.Roles.Where(r => requested.Contains(r.Name)).ToListAsync(ct);
		foreach (var role in roles)
			staff.StaffRoles.Add(new StaffRole { Role = role, Staff = staff });

		Db.Staff.Add(staff);
		try
		{
			await Db.SaveChangesAsync(ct);
		}
		catch (DbUpdateException ex) when (IsUniqueViolation(ex))
		{
			return null;
		}

		return ToUserInfo(staff);
	}
}
