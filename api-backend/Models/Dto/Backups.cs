using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using api_backend.Models.Backup;

namespace api_backend.Models.Dto;

/// <summary>
/// The automatic backup schedule (times UTC, <see cref="Weekday"/> 0 = Sunday like JS <c>getUTCDay</c>, used by weekly
/// and biweekly; <see cref="DayOfMonth"/> 1-28 by monthly) plus where
/// archives go: <see cref="Directory"/> in the container, <see cref="HostPath"/> its host side when the instance set it.
/// </summary>
public record BackupSettingsResponse(
	BackupInterval Interval,
	string TimeOfDay,
	int Weekday,
	int DayOfMonth,
	int Retention,
	DateTimeOffset? LastRunAt,
	string? LastError,
	DateTimeOffset? NextRunAt,
	string Directory,
	string? HostPath,
	long? FreeBytes,
	long MaxUploadBytes,
	int MaxAgeYears);

public record UpdateBackupSettingsRequest(
	BackupInterval Interval,
	[Required, RegularExpression("^([01][0-9]|2[0-3]):[0-5][0-9]$")] string TimeOfDay,
	[Range(0, 6)] int Weekday,
	[Range(1, 28)] int DayOfMonth,
	[Range(1, 100)] int Retention);

/// <summary>
/// An archive in the backup directory. <see cref="Kind"/> comes from the file name (uploads keep their original
/// manifest); <see cref="Error"/> is set when the manifest can't be read (the file can still be deleted).
/// </summary>
public record BackupInfo(
	string Name,
	BackupKind Kind,
	DateTimeOffset CreatedAt,
	long Size,
	string? CmsVersion,
	int? MediaCount,
	string? Error);

[JsonConverter(typeof(JsonStringEnumConverter<BackupKind>))]
public enum BackupKind
{
	[JsonStringEnumMemberName("auto")] Auto,
	[JsonStringEnumMemberName("manual")] Manual,
	[JsonStringEnumMemberName("pre-restore")] PreRestore,
	[JsonStringEnumMemberName("upload")] Upload,
}

/// <summary><see cref="PreRestoreBackup"/>: the backup of the state before the restore (restore it to undo).</summary>
public record RestoreResult(string PreRestoreBackup);
