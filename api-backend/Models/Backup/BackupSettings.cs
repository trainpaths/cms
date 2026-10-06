using System.Text.Json.Serialization;

namespace api_backend.Models.Backup;

/// <summary>
/// Schedule of the automatic full backups (single row, <see cref="SingletonId"/>). Its data is left out of the backups
/// themselves and carried over a restore, so restoring an old backup doesn't change the schedule. No staff foreign key
/// for the same reason: a restore replaces the staff table.
/// </summary>
public class BackupSettings
{
	public const int SingletonId = 1;

	public int Id { get; set; } = SingletonId;

	public BackupInterval Interval { get; set; } = BackupInterval.Off;

	/// <summary>UTC time of day the automatic backup runs.</summary>
	public TimeOnly TimeOfDay { get; set; } = new(3, 0);

	/// <summary>Weekly / biweekly backups: the day (UTC).</summary>
	public DayOfWeek Weekday { get; set; } = DayOfWeek.Sunday;

	/// <summary>Monthly backups: the day of the month (1-28, so every month has it).</summary>
	public int DayOfMonth { get; set; } = 1;

	/// <summary>How many automatic backups are kept; older ones are deleted after each run.</summary>
	public int Retention { get; set; } = 7;

	/// <summary>Start of the last automatic run (successful or not).</summary>
	public DateTimeOffset? LastRunAt { get; set; }

	/// <summary>Why the last automatic run failed; null after a success.</summary>
	public string? LastError { get; set; }

	public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

[JsonConverter(typeof(JsonStringEnumConverter<BackupInterval>))]
public enum BackupInterval
{
	[JsonStringEnumMemberName("off")] Off,
	[JsonStringEnumMemberName("daily")] Daily,
	[JsonStringEnumMemberName("weekly")] Weekly,
	/// <summary>Every other week: the first chosen weekday after the schedule was saved, then every 14 days.</summary>
	[JsonStringEnumMemberName("biweekly")] Biweekly,
	[JsonStringEnumMemberName("monthly")] Monthly,
}

/// <summary>Table names the backup code refers to in raw SQL / pg_dump arguments.</summary>
public static class BackupTables
{
	public const string Settings = "backup_settings";

	/// <summary>
	/// Tables whose rows a backup leaves out: the schedule (kept across a restore), sessions and one-time tokens
	/// (everyone signs in again after a restore) and pre-rendered HTML (re-rendered).
	/// </summary>
	public static readonly string[] DataExcluded = [Settings, "refresh_tokens", "verification_tokens", "rendered_pages"];
}
