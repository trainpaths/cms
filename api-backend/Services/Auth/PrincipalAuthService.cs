using api_backend.Models.Auth;
using api_backend.Models.Auth.JWT;
using api_backend.Models.Dto;
using api_backend.Services.Auth.JWT;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace api_backend.Services.Auth;

/// <summary>Response body plus the refresh token, which the controller puts in a cookie.</summary>
public record AuthResult(AuthResponse Response, IssuedRefreshToken Refresh)
{
	public string AccessToken => Response.AccessToken;
	public UserInfo User => Response.User;
}

/// <summary>Auth flows shared by <see cref="CustomerAuthService"/> and <see cref="StaffAuthService"/>.</summary>
public abstract class PrincipalAuthService<TUser>(
	AppDbContext db,
	PasswordHashService hasher,
	JwtTokenService tokens,
	RefreshTokenService refreshTokens)
	where TUser : class, IAuthPrincipal
{
	protected AppDbContext Db { get; } = db;
	protected PasswordHashService Hasher { get; } = hasher;
	protected JwtTokenService Tokens { get; } = tokens;
	protected RefreshTokenService RefreshTokens { get; } = refreshTokens;

	protected abstract UserType Type { get; }

	protected virtual IQueryable<TUser> Users => Db.Set<TUser>();

	protected abstract UserInfo ToUserInfo(TUser user);
	protected abstract AccessToken CreateAccessToken(TUser user);

	public async Task<AuthResult?> LoginAsync(LoginRequest req, string? ip, CancellationToken ct = default)
	{
		var email = NormalizeEmail(req.Email);
		var user = await Users.FirstOrDefaultAsync(u => u.Email == email, ct);
		if (user is null || !user.IsActive)
		{
			Hasher.VerifyDummy(req.Password);
			return null;
		}

		if (!Hasher.Verify(user.PasswordHash, req.Password, out var needsRehash)) return null;
		if (needsRehash) user.PasswordHash = Hasher.Hash(req.Password);

		user.LastLoginAt = DateTimeOffset.UtcNow;
		return await IssueAsync(user, ip, ct);
	}

	public async Task<AuthResult?> RefreshAsync(string? rawRefreshToken, string? ip, CancellationToken ct = default)
	{
		if (string.IsNullOrEmpty(rawRefreshToken)) return null;

		var rotation = await RefreshTokens.RotateAsync(rawRefreshToken, Type, ct);
		if (rotation is null) return null;

		var user = await Users.FirstOrDefaultAsync(u => u.Id == rotation.UserId, ct);
		if (user is null || !user.IsActive) return null;

		return await IssueAsync(user, ip, ct, rotation.ReplacementId);
	}

	public Task<bool> LogoutAsync(string? rawRefreshToken, CancellationToken ct = default) =>
		string.IsNullOrEmpty(rawRefreshToken)
			? Task.FromResult(false)
			: RefreshTokens.RevokeAsync(rawRefreshToken, Type, ct);

	public async Task<bool> ChangePasswordAsync(
		Guid userId, string currentPassword, string newPassword, CancellationToken ct = default)
	{
		var user = await Db.Set<TUser>().FirstOrDefaultAsync(u => u.Id == userId, ct);
		if (user is null || !user.IsActive) return false;
		if (!Hasher.Verify(user.PasswordHash, currentPassword)) return false;

		user.PasswordHash = Hasher.Hash(newPassword);
		user.UpdatedAt = DateTimeOffset.UtcNow;

		await RefreshTokens.RevokeAllAsync(Type, userId, ct);
		await Db.SaveChangesAsync(ct);
		return true;
	}

	public async Task<UserInfo?> UpdateProfileAsync(Guid userId, string? displayName, CancellationToken ct = default)
	{
		var user = await Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
		if (user is null || !user.IsActive) return null;

		user.DisplayName = displayName;
		user.UpdatedAt = DateTimeOffset.UtcNow;
		await Db.SaveChangesAsync(ct);
		return ToUserInfo(user);
	}

	public async Task<UserInfo?> GetMeAsync(Guid userId, CancellationToken ct = default)
	{
		var user = await Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct);
		return user is null ? null : ToUserInfo(user);
	}

	// Single SaveChanges for everything pending (new user, LastLoginAt, refresh token).
	protected async Task<AuthResult> IssueAsync(
		TUser user, string? ip, CancellationToken ct, Guid? refreshTokenId = null)
	{
		var access = CreateAccessToken(user);
		var refresh = RefreshTokens.Add(Type, user.Id, ip, refreshTokenId);
		await Db.SaveChangesAsync(ct);

		return new AuthResult(new AuthResponse(access.Token, access.ExpiresAt, ToUserInfo(user)), refresh);
	}

	protected static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

	protected static bool IsUniqueViolation(DbUpdateException ex) =>
		ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
