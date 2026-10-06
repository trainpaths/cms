using System.Data;
using System.Formats.Tar;
using System.IO.Compression;
using System.Text.Json;
using api_backend.Models.Backup;
using api_backend.Models.Dto;
using api_backend.Services.Media;
using api_backend.Services.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace api_backend.Services.Backup;

public enum BackupError
{
	NotFound,
	Busy,
	Invalid,
}

public record BackupResult<T>(T? Value, BackupError? Error = null, string? Detail = null)
{
	public static BackupResult<T> Ok(T value) => new(value);
	public static BackupResult<T> Fail(BackupError error, string? detail = null) => new(default, error, detail);
}

/// <summary>
/// Full site backups: the database (pg_dump) and every media blob in one archive in <see cref="BackupOptions.Directory"/>,
/// which is the source of truth for the list (no table). Design + failure handling: claude-context/ARCHITECTURE.md → Backups.
/// </summary>
public sealed class BackupService(
	AppDbContext db,
	IBlobStorage blobs,
	IDatabaseDumper dumper,
	BackupLock backupLock,
	IRenderQueue render,
	IOptions<BackupOptions> options,
	IConfiguration config,
	ILogger<BackupService> logger)
{
	private const string WorkDir = ".work";
	private const string BusyMessage = "Another backup or restore is running.";

	private string Dir => options.Value.Directory;
	private string ConnectionString => config.GetConnectionString("Postgres")!;

	public long MaxUploadBytes => options.Value.MaxUploadMegabytes * 1024 * 1024;

	// ── settings ───────────────────────────────────────────────────────────────

	public async Task<BackupSettingsResponse> GetSettingsAsync(CancellationToken ct) =>
		ToResponse(await LoadSettingsAsync(ct));

	public async Task<BackupSettingsResponse> UpdateSettingsAsync(UpdateBackupSettingsRequest req, CancellationToken ct)
	{
		var settings = await db.BackupSettings.FirstOrDefaultAsync(ct);
		if (settings is null)
		{
			settings = new BackupSettings();
			db.BackupSettings.Add(settings);
		}
		settings.Interval = req.Interval;
		settings.TimeOfDay = TimeOnly.ParseExact(req.TimeOfDay, "HH:mm");
		settings.Weekday = (DayOfWeek)req.Weekday;
		settings.Retention = req.Retention;
		settings.UpdatedAt = DateTimeOffset.UtcNow;
		await db.SaveChangesAsync(ct);
		return ToResponse(settings);
	}

	private async Task<BackupSettings> LoadSettingsAsync(CancellationToken ct) =>
		await db.BackupSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new BackupSettings();

	private BackupSettingsResponse ToResponse(BackupSettings s)
	{
		long? free = null;
		try
		{
			if (Directory.Exists(Dir))
				free = new DriveInfo(Path.GetFullPath(Dir)).AvailableFreeSpace;
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
		{
		}
		return new(s.Interval, s.TimeOfDay.ToString("HH:mm"), (int)s.Weekday, s.Retention, s.LastRunAt, s.LastError,
			BackupSchedule.NextRun(s, DateTimeOffset.UtcNow), Dir,
			string.IsNullOrWhiteSpace(options.Value.HostPath) ? null : options.Value.HostPath, free, MaxUploadBytes);
	}

	// ── list / read / delete ───────────────────────────────────────────────────

	public async Task<List<BackupInfo>> ListAsync(CancellationToken ct)
	{
		if (!Directory.Exists(Dir))
			return [];
		var list = new List<BackupInfo>();
		foreach (var path in Directory.EnumerateFiles(Dir))
		{
			var name = Path.GetFileName(path);
			if (BackupArchive.NamePattern().IsMatch(name))
				list.Add(await InfoAsync(path, ct));
		}
		return list.OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Name).ToList();
	}

	/// <summary>Null for unknown or malformed names (no path traversal: names never reach the file system unchecked).</summary>
	public string? PathOf(string name) =>
		BackupArchive.NamePattern().IsMatch(name) && File.Exists(Path.Combine(Dir, name)) ? Path.Combine(Dir, name) : null;

	public BackupResult<bool> Delete(string name)
	{
		using var held = backupLock.TryEnter();
		if (held is null)
			return BackupResult<bool>.Fail(BackupError.Busy, BusyMessage);
		if (PathOf(name) is not { } path)
			return BackupResult<bool>.Fail(BackupError.NotFound);
		File.Delete(path);
		return BackupResult<bool>.Ok(true);
	}

	private static async Task<BackupInfo> InfoAsync(string path, CancellationToken ct)
	{
		var name = Path.GetFileName(path);
		var file = new FileInfo(path);
		try
		{
			var m = await BackupArchive.ReadManifestAsync(path, ct);
			return new(name, BackupArchive.KindOf(name), m.CreatedAt, file.Length, m.CmsVersion, m.MediaCount, null);
		}
		catch (Exception e) when (e is BackupFailedException or InvalidDataException or IOException or FormatException)
		{
			return new(name, BackupArchive.KindOf(name), file.LastWriteTimeUtc, file.Length, null, null,
				e is BackupFailedException ? e.Message : "The backup file is damaged.");
		}
	}

	// ── create ─────────────────────────────────────────────────────────────────

	public async Task<BackupResult<BackupInfo>> CreateAsync(BackupKind kind, CancellationToken ct)
	{
		using var held = backupLock.TryEnter();
		if (held is null)
			return BackupResult<BackupInfo>.Fail(BackupError.Busy, BusyMessage);
		return BackupResult<BackupInfo>.Ok(await CreateLockedAsync(kind, ct));
	}

	/// <summary>
	/// Database and media from one point in time: the snapshot exported by this transaction is what pg_dump dumps,
	/// and the media key list is read inside it. A blob deleted before it's copied is listed in missing-media.json.
	/// </summary>
	private async Task<BackupInfo> CreateLockedAsync(BackupKind kind, CancellationToken ct)
	{
		var work = CreateWorkDir();
		var name = NewName(kind);
		var final = Path.Combine(Dir, name);
		var partial = final + ".partial";
		try
		{
			var dumpFile = Path.Combine(work, BackupArchive.DumpEntry);
			List<string> keys;
			string? lastMigration;
			await using (var conn = new NpgsqlConnection(ConnectionString))
			{
				await conn.OpenAsync(ct);
				await using var tx = await conn.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
				var snapshot = (string)(await Scalar(conn, "SELECT pg_export_snapshot()", ct))!;
				keys = await Strings(conn, """SELECT "StorageKey" FROM media_assets ORDER BY "StorageKey" """, ct);
				lastMigration = (string?)await Scalar(conn,
					"""SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1""", ct);
				// the snapshot is importable only while this transaction is open
				await dumper.DumpAsync(ConnectionString, snapshot, dumpFile, ct);
			}

			var manifest = new BackupManifest(BackupArchive.Format, BackupArchive.Version, DateTimeOffset.UtcNow, kind,
				config["Cms:Version"], lastMigration, keys.Count);
			var missing = new List<string>();
			await using (var file = File.Create(partial))
			{
				// dump and images are compressed already
				await using var gzip = new GZipStream(file, CompressionLevel.Fastest);
				await using var tar = new TarWriter(gzip, TarEntryFormat.Pax);
				await WriteEntryAsync(tar, BackupArchive.ManifestEntry, JsonSerializer.SerializeToUtf8Bytes(manifest, BackupArchive.Json), ct);
				await tar.WriteEntryAsync(dumpFile, BackupArchive.DumpEntry, ct);
				foreach (var key in keys)
				{
					await using var blob = await blobs.GetAsync(key, ct);
					if (blob is null)
					{
						missing.Add(key);
						continue;
					}
					// TarWriter needs the length up front; blobs are ≤ 10 MB (MediaService.MaxUploadBytes)
					using var buffer = new MemoryStream();
					await blob.Content.CopyToAsync(buffer, ct);
					await WriteEntryAsync(tar, BackupArchive.MediaPrefix + key, buffer.ToArray(), ct);
				}
				if (missing.Count > 0)
				{
					await WriteEntryAsync(tar, BackupArchive.MissingMediaEntry, JsonSerializer.SerializeToUtf8Bytes(missing), ct);
					logger.LogWarning("Backup {Name}: {Count} media blobs were missing: {Keys}", name, missing.Count, missing);
				}
			}
			File.Move(partial, final);
			logger.LogInformation("Backup {Name} created ({Media} media files).", name, keys.Count - missing.Count);
			return await InfoAsync(final, ct);
		}
		finally
		{
			TryDelete(partial);
			TryDeleteDir(work);
		}
	}

	// ── upload ─────────────────────────────────────────────────────────────────

	/// <summary>Stores an archive from elsewhere as <c>upload-…</c> after reading it fully (catches damage early).</summary>
	public async Task<BackupResult<BackupInfo>> SaveUploadAsync(Stream content, CancellationToken ct)
	{
		using var held = backupLock.TryEnter();
		if (held is null)
			return BackupResult<BackupInfo>.Fail(BackupError.Busy, BusyMessage);
		var work = CreateWorkDir();
		try
		{
			var temp = Path.Combine(work, "upload" + BackupArchive.Extension);
			await using (var file = File.Create(temp))
				await content.CopyToAsync(file, ct);
			try
			{
				await BackupArchive.ScanAsync(temp, null, null, ct);
			}
			catch (BackupFailedException e)
			{
				return BackupResult<BackupInfo>.Fail(BackupError.Invalid, e.Message);
			}
			var final = Path.Combine(Dir, NewName(BackupKind.Upload));
			File.Move(temp, final);
			return BackupResult<BackupInfo>.Ok(await InfoAsync(final, ct));
		}
		finally
		{
			TryDeleteDir(work);
		}
	}

	// ── restore ────────────────────────────────────────────────────────────────

	/// <summary>
	/// Replaces the database and media with the archive's. Order matters: check everything first, take a
	/// <c>pre-restore</c> backup, then (API in maintenance) upload the blobs (keys are unique: additive), swap the database
	/// in one transaction, migrate, carry the backup schedule over and drop blobs nothing references any more.
	/// </summary>
	public async Task<BackupResult<string>> RestoreAsync(string name, CancellationToken ct)
	{
		using var held = backupLock.TryEnter();
		if (held is null)
			return BackupResult<string>.Fail(BackupError.Busy, BusyMessage);
		if (PathOf(name) is not { } path)
			return BackupResult<string>.Fail(BackupError.NotFound);

		var work = CreateWorkDir();
		try
		{
			var dumpFile = Path.Combine(work, BackupArchive.DumpEntry);
			BackupManifest manifest;
			try
			{
				manifest = await BackupArchive.ScanAsync(path, dumpFile, null, ct);
			}
			catch (BackupFailedException e)
			{
				return BackupResult<string>.Fail(BackupError.Invalid, e.Message);
			}
			if (manifest.LastMigration is { } migration && !db.Database.GetMigrations().Contains(migration))
				return BackupResult<string>.Fail(BackupError.Invalid,
					$"The backup was made by a newer CMS version (database migration {migration} is unknown here). Upgrade first.");

			var schedule = await LoadSettingsAsync(ct);
			var safety = await CreateLockedAsync(BackupKind.PreRestore, ct);
			logger.LogWarning("Restoring backup {Name}; the current state was saved as {Safety}.", name, safety.Name);

			using (backupLock.Maintenance())
			{
				await BackupArchive.ScanAsync(path, null, async (key, data) =>
				{
					using var buffer = new MemoryStream();
					await data.CopyToAsync(buffer, ct);
					buffer.Position = 0;
					await blobs.PutAsync(key, buffer, MediaService.ContentTypeOf(key), ct);
				}, ct);

				try
				{
					await dumper.ReplaceAsync(ConnectionString, dumpFile, ct);
				}
				catch (BackupFailedException e)
				{
					throw new BackupFailedException(
						$"Restoring the database failed, nothing was changed. {e.Message}", e);
				}
				// pooled connections still point at the dropped schema's objects
				NpgsqlConnection.ClearAllPools();
				await db.Database.MigrateAsync(ct);

				db.ChangeTracker.Clear();
				await db.BackupSettings.ExecuteDeleteAsync(ct);
				db.BackupSettings.Add(schedule);
				await db.SaveChangesAsync(ct);

				await DeleteUnreferencedBlobsAsync(ct);
				render.All();
			}
			logger.LogWarning("Backup {Name} restored.", name);
			return BackupResult<string>.Ok(safety.Name);
		}
		finally
		{
			TryDeleteDir(work);
		}
	}

	private async Task DeleteUnreferencedBlobsAsync(CancellationToken ct)
	{
		var referenced = (await db.MediaAssets.Select(m => m.StorageKey).ToListAsync(ct)).ToHashSet();
		var orphans = new List<string>();
		await foreach (var key in blobs.ListKeysAsync(ct))
		{
			if (MediaService.StorageKeyPattern().IsMatch(key) && !referenced.Contains(key))
				orphans.Add(key);
		}
		foreach (var key in orphans)
			await blobs.DeleteAsync(key, ct);
		if (orphans.Count > 0)
			logger.LogInformation("Restore removed {Count} media files the restored site doesn't use.", orphans.Count);
	}

	// ── schedule ───────────────────────────────────────────────────────────────

	/// <summary>Runs the automatic backup when due (called every minute by <see cref="BackupScheduler"/>).</summary>
	public async Task RunScheduledAsync(DateTimeOffset now, CancellationToken ct)
	{
		var settings = await db.BackupSettings.FirstOrDefaultAsync(ct);
		if (settings is null || !BackupSchedule.IsDue(settings, now))
			return;
		using var held = backupLock.TryEnter();
		if (held is null)
			return; // busy: still due next minute

		settings.LastRunAt = now;
		try
		{
			await CreateLockedAsync(BackupKind.Auto, ct);
			Prune(settings.Retention);
			settings.LastError = null;
		}
		catch (Exception e) when (e is not OperationCanceledException)
		{
			logger.LogError(e, "Automatic backup failed.");
			settings.LastError = e is BackupFailedException or IOException or UnauthorizedAccessException
				? Truncate(e.Message, 2000)
				: "Unexpected error, see the API logs.";
		}
		await db.SaveChangesAsync(ct);
	}

	private void Prune(int keep)
	{
		var autos = Directory.EnumerateFiles(Dir, "auto-*" + BackupArchive.Extension)
			.Select(Path.GetFileName)
			.Where(n => BackupArchive.NamePattern().IsMatch(n!))
			.OrderDescending(StringComparer.Ordinal)
			.Skip(keep);
		foreach (var name in autos)
		{
			File.Delete(Path.Combine(Dir, name!));
			logger.LogInformation("Deleted old automatic backup {Name}.", name);
		}
	}

	// ── helpers ────────────────────────────────────────────────────────────────

	private string NewName(BackupKind kind)
	{
		var stem = $"{BackupArchive.Prefix(kind)}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";
		var name = stem + BackupArchive.Extension;
		for (var i = 2; File.Exists(Path.Combine(Dir, name)); i++)
			name = $"{stem}-{i}{BackupArchive.Extension}";
		return name;
	}

	private string CreateWorkDir()
	{
		try
		{
			return Directory.CreateDirectory(Path.Combine(Dir, WorkDir, Guid.NewGuid().ToString("N"))).FullName;
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			throw new BackupFailedException($"The backup directory {Dir} isn't writable ({e.Message}).", e);
		}
	}

	private static async Task WriteEntryAsync(TarWriter tar, string name, byte[] data, CancellationToken ct)
	{
		var entry = new PaxTarEntry(TarEntryType.RegularFile, name) { DataStream = new MemoryStream(data) };
		await tar.WriteEntryAsync(entry, ct);
	}

	private static async Task<object?> Scalar(NpgsqlConnection conn, string sql, CancellationToken ct)
	{
		await using var cmd = new NpgsqlCommand(sql, conn);
		return await cmd.ExecuteScalarAsync(ct);
	}

	private static async Task<List<string>> Strings(NpgsqlConnection conn, string sql, CancellationToken ct)
	{
		await using var cmd = new NpgsqlCommand(sql, conn);
		await using var reader = await cmd.ExecuteReaderAsync(ct);
		var list = new List<string>();
		while (await reader.ReadAsync(ct))
			list.Add(reader.GetString(0));
		return list;
	}

	private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

	private void TryDelete(string path)
	{
		try
		{
			File.Delete(path);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			logger.LogWarning(e, "Could not delete {Path}.", path);
		}
	}

	private void TryDeleteDir(string path)
	{
		try
		{
			if (Directory.Exists(path))
				Directory.Delete(path, recursive: true);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			logger.LogWarning(e, "Could not delete {Path}.", path);
		}
	}
}
