using api_backend.Models.Backup;
using api_backend.Services.Backup;
using AwesomeAssertions;

namespace api_backend.Tests.Unit.Services;

public class BackupScheduleTests
{
	// Wednesday
	private static readonly DateTimeOffset Today = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);

	private static BackupSettings Daily(DateTimeOffset updatedAt, DateTimeOffset? lastRun = null) => new()
	{
		Interval = BackupInterval.Daily,
		TimeOfDay = new TimeOnly(3, 0),
		UpdatedAt = updatedAt,
		LastRunAt = lastRun,
	};

	[Fact]
	public void Off_IsNeverDue()
	{
		var s = new BackupSettings { Interval = BackupInterval.Off, UpdatedAt = Today.AddDays(-30) };
		BackupSchedule.IsDue(s, Today.AddHours(12)).Should().BeFalse();
		BackupSchedule.NextRun(s, Today.AddHours(12)).Should().BeNull();
	}

	[Fact]
	public void Daily_DueOncePerSlot()
	{
		var s = Daily(updatedAt: Today.AddDays(-1));
		var now = Today.AddHours(3).AddSeconds(30);

		BackupSchedule.IsDue(s, Today.AddHours(2)).Should().BeTrue("yesterday's 03:00 slot came after the settings were saved");
		BackupSchedule.IsDue(s, now).Should().BeTrue();

		s.LastRunAt = now;
		BackupSchedule.IsDue(s, now.AddMinutes(1)).Should().BeFalse();
		BackupSchedule.NextRun(s, now.AddMinutes(1)).Should().Be(Today.AddDays(1).AddHours(3));
	}

	[Fact]
	public void SlotsBeforeSaving_DontCount()
	{
		// turned on at 10:00 with a 03:00 slot: waits for tomorrow
		var s = Daily(updatedAt: Today.AddHours(10));
		BackupSchedule.IsDue(s, Today.AddHours(10).AddMinutes(1)).Should().BeFalse();
		BackupSchedule.NextRun(s, Today.AddHours(10).AddMinutes(1)).Should().Be(Today.AddDays(1).AddHours(3));
		BackupSchedule.IsDue(s, Today.AddDays(1).AddHours(3)).Should().BeTrue();
	}

	[Fact]
	public void Downtime_CatchesUpWithOneRun()
	{
		var s = Daily(updatedAt: Today.AddDays(-10), lastRun: Today.AddDays(-4).AddHours(3));
		var now = Today.AddHours(9);
		BackupSchedule.IsDue(s, now).Should().BeTrue();
		BackupSchedule.NextRun(s, now).Should().Be(now);

		s.LastRunAt = now;
		BackupSchedule.IsDue(s, now.AddMinutes(1)).Should().BeFalse();
	}

	[Fact]
	public void Weekly_UsesTheWeekday()
	{
		var s = new BackupSettings
		{
			Interval = BackupInterval.Weekly,
			Weekday = DayOfWeek.Monday,
			TimeOfDay = new TimeOnly(3, 0),
			UpdatedAt = Today.AddDays(-30),
		};
		var monday = Today.AddDays(-2).AddHours(3);

		BackupSchedule.LastSlot(s, Today.AddHours(12)).Should().Be(monday);
		BackupSchedule.LastSlot(s, monday).Should().Be(monday);
		BackupSchedule.LastSlot(s, monday.AddSeconds(-1)).Should().Be(monday.AddDays(-7));

		s.LastRunAt = monday.AddSeconds(5);
		BackupSchedule.NextRun(s, Today.AddHours(12)).Should().Be(monday.AddDays(7));
	}

	[Fact]
	public void Biweekly_EveryOtherWeek_FromTheFirstWeekdayAfterSaving()
	{
		// saved Wednesday 10:00; Monday slots: the 12th is the first, then the 26th
		var s = new BackupSettings
		{
			Interval = BackupInterval.Biweekly,
			Weekday = DayOfWeek.Monday,
			TimeOfDay = new TimeOnly(3, 0),
			UpdatedAt = Today.AddHours(10),
		};
		var first = new DateTimeOffset(2026, 10, 12, 3, 0, 0, TimeSpan.Zero);

		BackupSchedule.IsDue(s, first.AddSeconds(-1)).Should().BeFalse();
		BackupSchedule.NextRun(s, Today.AddHours(11)).Should().Be(first);
		BackupSchedule.IsDue(s, first).Should().BeTrue();

		s.LastRunAt = first;
		BackupSchedule.IsDue(s, first.AddDays(7)).Should().BeFalse("the Monday in between is skipped");
		BackupSchedule.NextRun(s, first.AddDays(1)).Should().Be(first.AddDays(14));
		BackupSchedule.IsDue(s, first.AddDays(14)).Should().BeTrue();
	}

	[Fact]
	public void Monthly_OnTheDayOfMonth()
	{
		var s = new BackupSettings
		{
			Interval = BackupInterval.Monthly,
			DayOfMonth = 15,
			TimeOfDay = new TimeOnly(3, 0),
			UpdatedAt = Today.AddDays(-60),
		};
		var october = new DateTimeOffset(2026, 10, 15, 3, 0, 0, TimeSpan.Zero);

		BackupSchedule.LastSlot(s, Today).Should().Be(october.AddMonths(-1), "the 15th of October hasn't come yet");
		s.LastRunAt = october.AddMonths(-1);
		BackupSchedule.NextRun(s, Today).Should().Be(october);
		BackupSchedule.IsDue(s, october).Should().BeTrue();

		s.LastRunAt = october;
		BackupSchedule.NextRun(s, october.AddDays(1)).Should().Be(new DateTimeOffset(2026, 11, 15, 3, 0, 0, TimeSpan.Zero));
	}

	[Fact]
	public void ArchiveNames_CarryWhenTheyWereStored()
	{
		BackupArchive.StoredAt("pre-restore-20240229-235959.tar.gz").Should().Be(new DateTimeOffset(2024, 2, 29, 23, 59, 59, TimeSpan.Zero));
		BackupArchive.StoredAt("manual-20261006-120000-2.tar.gz").Should().Be(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
		BackupArchive.StoredAt("notes.txt").Should().BeNull();
	}
}
