using api_backend.Models.Backup;

namespace api_backend.Services.Backup;

/// <summary>
/// When automatic backups are due. A slot is a time of day (UTC) on every day, a weekday (every week or every other
/// week) or a day of the month. A slot is due once it has passed and nothing ran since; slots before the settings were
/// last saved don't count, so turning backups on at 10:00 with a 03:00 slot waits for tomorrow instead of running at
/// once. A server that was down catches up with one run.
/// </summary>
public static class BackupSchedule
{
	/// <summary>The latest slot at or before <paramref name="now"/>; null when off.</summary>
	public static DateTimeOffset? LastSlot(BackupSettings s, DateTimeOffset now)
	{
		var utc = now.ToUniversalTime();
		var today = new DateTimeOffset(DateOnly.FromDateTime(utc.UtcDateTime), s.TimeOfDay, TimeSpan.Zero);
		switch (s.Interval)
		{
			case BackupInterval.Daily:
				return today > utc ? today.AddDays(-1) : today;
			case BackupInterval.Weekly:
				return LatestWeekday(s, utc);
			case BackupInterval.Biweekly:
			{
				// every 14 days from the first matching weekday after saving
				var first = LatestWeekday(s, s.UpdatedAt.ToUniversalTime()).AddDays(7);
				if (utc < first)
					return first.AddDays(-14);
				return first.AddDays(14 * Math.Floor((utc - first).TotalDays / 14));
			}
			case BackupInterval.Monthly:
			{
				var slot = new DateTimeOffset(utc.Year, utc.Month, s.DayOfMonth, s.TimeOfDay.Hour, s.TimeOfDay.Minute, 0, TimeSpan.Zero);
				return slot > utc ? slot.AddMonths(-1) : slot;
			}
			default:
				return null;
		}
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
		if (IsDue(s, now))
			return now;
		return s.Interval switch
		{
			BackupInterval.Weekly => last.AddDays(7),
			BackupInterval.Biweekly => last.AddDays(14),
			BackupInterval.Monthly => last.AddMonths(1),
			_ => last.AddDays(1),
		};
	}

	// the latest weekday-at-time slot at or before `at`
	private static DateTimeOffset LatestWeekday(BackupSettings s, DateTimeOffset at)
	{
		var slot = new DateTimeOffset(DateOnly.FromDateTime(at.UtcDateTime), s.TimeOfDay, TimeSpan.Zero);
		if (slot > at)
			slot = slot.AddDays(-1);
		return slot.AddDays(-(((int)slot.DayOfWeek - (int)s.Weekday + 7) % 7));
	}
}
