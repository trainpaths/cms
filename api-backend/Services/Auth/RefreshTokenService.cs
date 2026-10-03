using api_backend.Models.Auth.JWT;
using api_backend.Services.Auth.JWT;
using Microsoft.EntityFrameworkCore;

namespace api_backend.Services.Auth;

public record IssuedRefreshToken(string RawToken, DateTimeOffset ExpiresAt);

public record RefreshTokenRotation(Guid UserId, Guid ReplacementId);

/// <summary>Refresh-token issue, rotation (with reuse detection) and revocation for all principals.</summary>
public class RefreshTokenService(AppDbContext db, JwtTokenService tokens, ILogger<RefreshTokenService> logger)
{
	// Re-presenting a token this soon after rotation is a race (e.g. two tabs), not theft.
	private static readonly TimeSpan ReuseGracePeriod = TimeSpan.FromSeconds(30);

	/// <summary>Stages a token; the caller saves.</summary>
	public IssuedRefreshToken Add(UserType type, Guid userId, string? ip, Guid? id = null)
	{
		var generated = tokens.CreateRefreshToken();
		db.RefreshTokens.Add(new RefreshToken
		{
			Id = id ?? Guid.NewGuid(),
			TokenHash = generated.TokenHash,
			UserType = type,
			CustomerId = type == UserType.Customer ? userId : null,
			StaffId = type == UserType.Staff ? userId : null,
			ExpiresAt = generated.ExpiresAt,
			CreatedByIp = ip,
		});
		return new IssuedRefreshToken(generated.RawToken, generated.ExpiresAt);
	}

	/// <summary>
	/// Revokes the token and reserves its successor's id; null if invalid or a concurrent rotation won.
	/// Reuse of an already rotated token revokes all of the user's sessions.
	/// </summary>
	public async Task<RefreshTokenRotation?> RotateAsync(string rawToken, UserType type, CancellationToken ct = default)
	{
		var hash = tokens.HashRefreshToken(rawToken);
		var existing = await db.RefreshTokens
			.AsNoTracking()
			.FirstOrDefaultAsync(t => t.TokenHash == hash && t.UserType == type, ct);
		if (existing is null) return null;

		var userId = (existing.CustomerId ?? existing.StaffId)!.Value;
		var now = DateTimeOffset.UtcNow;

		if (existing.RevokedAt is { } revokedAt)
		{
			if (existing.ReplacedByTokenId is not null && now - revokedAt > ReuseGracePeriod)
			{
				logger.LogWarning(
					"Refresh token reuse detected for {UserType} {UserId}; revoking all sessions.", type, userId);
				await RevokeAllAsync(type, userId, ct);
			}
			return null;
		}

		if (existing.ExpiresAt <= now) return null;

		// Atomic claim: only one concurrent rotation updates the row.
		var replacementId = Guid.NewGuid();
		var claimed = await db.RefreshTokens
			.Where(t => t.Id == existing.Id && t.RevokedAt == null)
			.ExecuteUpdateAsync(s => s
				.SetProperty(t => t.RevokedAt, now)
				.SetProperty(t => t.ReplacedByTokenId, (Guid?)replacementId), ct);

		return claimed == 1 ? new RefreshTokenRotation(userId, replacementId) : null;
	}

	public async Task<bool> RevokeAsync(string rawToken, UserType type, CancellationToken ct = default)
	{
		var hash = tokens.HashRefreshToken(rawToken);
		var token = await db.RefreshTokens
			.FirstOrDefaultAsync(t => t.TokenHash == hash && t.UserType == type, ct);
		if (token is null || !token.IsActive) return false;

		token.RevokedAt = DateTimeOffset.UtcNow;
		await db.SaveChangesAsync(ct);
		return true;
	}

	public Task<int> RevokeAllAsync(UserType type, Guid userId, CancellationToken ct = default)
	{
		var now = DateTimeOffset.UtcNow;
		var active = db.RefreshTokens.Where(t => t.RevokedAt == null && t.ExpiresAt > now);
		active = type == UserType.Customer
			? active.Where(t => t.CustomerId == userId)
			: active.Where(t => t.StaffId == userId);
		return active.ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
	}
}
