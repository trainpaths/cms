using api_backend.Models.Backup;

namespace api_backend.Services.Backup;

/// <summary>
/// When automatic backups are due. A slot is a daily time (or weekday + time), UTC. A slot is due once it has passed
/// and nothing ran since; slots before the settings were last saved don't count, so turning backups on at 10:00 with
/// a 03:00 slot waits for tomorrow instead of running at once. A server that was down catches up with one run.
/// </summary>
public static class BackupSchedule
{
	/// <summary>The latest slot at or before <paramref name="now"/>; null when off.</summary>
	public static DateTimeOffset? LastSlot(BackupSettings s, DateTimeOffset now)
	{
		if (s.Interval == BackupInterval.Off)
			return null;
		var utc = now.ToUniversalTime();
		var slot = new DateTimeOffset(DateOnly.FromDateTime(utc.UtcDateTime), s.TimeOfDay, TimeSpan.Zero);
		if (slot > utc)
			slot = slot.AddDays(-1);
		if (s.Interval == BackupInterval.Weekly)
			slot = slot.AddDays(-(((int)slot.DayOfWeek - (int)s.Weekday + 7) % 7));
		return slot;
	}

	public static bool IsDue(BackupSettings s, DateTimeOffset now) =>
		LastSlot(s, now) is { } slot
		&& slot > s.UpdatedAt
		&& (s.LastRunAt is null || slot > s.LastRunAt);

	/// <summary>When the next automatic backup starts (now, if one is due); null when off.</summary>
	public static DateTimeOffset? NextRun(BackupSettings s, DateTimeOffset now)
	{
		if (LastSlot(s, now) is not { } last)
			return null;
		return IsDue(s, now) ? now : last.AddDays(s.Interval == BackupInterval.Weekly ? 7 : 1);
	}
}
