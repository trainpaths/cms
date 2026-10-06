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
}
