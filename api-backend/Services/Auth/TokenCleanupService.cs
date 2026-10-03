using Microsoft.EntityFrameworkCore;

namespace api_backend.Services.Auth;

/// <summary>Deletes tokens that expired or were revoked/consumed more than <see cref="Retention"/> ago.</summary>
public sealed class TokenCleanupService(
	IServiceScopeFactory scopeFactory,
	ILogger<TokenCleanupService> logger) : BackgroundService
{
	private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
	public static readonly TimeSpan Retention = TimeSpan.FromDays(7);

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using var timer = new PeriodicTimer(Interval);
		while (await timer.WaitForNextTickAsync(stoppingToken))
		{
			try
			{
				using var scope = scopeFactory.CreateScope();
				var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
				var (refresh, verification) = await PurgeAsync(db, DateTimeOffset.UtcNow, stoppingToken);
				logger.LogInformation(
					"Token cleanup removed {RefreshTokens} refresh and {VerificationTokens} verification tokens.",
					refresh, verification);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				logger.LogError(ex, "Token cleanup failed.");
			}
		}
	}

	public static async Task<(int RefreshTokens, int VerificationTokens)> PurgeAsync(
		AppDbContext db, DateTimeOffset now, CancellationToken ct = default)
	{
		var cutoff = now - Retention;

		var refresh = await db.RefreshTokens
			.Where(t => t.ExpiresAt < cutoff || t.RevokedAt < cutoff)
			.ExecuteDeleteAsync(ct);

		var verification = await db.VerificationTokens
			.Where(t => t.ExpiresAt < cutoff || t.ConsumedAt < cutoff)
			.ExecuteDeleteAsync(ct);

		return (refresh, verification);
	}
}
