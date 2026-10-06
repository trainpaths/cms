namespace api_backend.Services.Backup;

/// <summary>
/// Every minute: deletes archives older than <see cref="BackupService.MaxAgeYears"/> and runs the automatic backup when
/// due (<see cref="BackupSchedule"/>).
/// </summary>
public sealed class BackupScheduler(IServiceScopeFactory scopeFactory, ILogger<BackupScheduler> logger) : BackgroundService
{
	private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using var timer = new PeriodicTimer(Interval);
		while (await timer.WaitForNextTickAsync(stoppingToken))
		{
			try
			{
				using var scope = scopeFactory.CreateScope();
				var backups = scope.ServiceProvider.GetRequiredService<BackupService>();
				var now = DateTimeOffset.UtcNow;
				backups.DeleteExpired(now);
				await backups.RunScheduledAsync(now, stoppingToken);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				// e.g. the database is briefly unreachable (or mid-restore): next tick retries
				logger.LogError(ex, "Backup schedule check failed.");
			}
		}
	}
}
