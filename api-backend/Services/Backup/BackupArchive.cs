using System.Formats.Tar;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using api_backend.Models.Dto;
using api_backend.Services.Media;

namespace api_backend.Services.Backup;

/// <summary>First entry of every archive; read alone for the backup list.</summary>
public sealed record BackupManifest(
	string Format,
	int Version,
	DateTimeOffset CreatedAt,
	BackupKind Kind,
	string? CmsVersion,
	string? LastMigration,
	int MediaCount);

/// <summary>
/// Archive layout (<c>&lt;kind&gt;-yyyyMMdd-HHmmss.tar.gz</c>): <c>manifest.json</c>, <c>db.dump</c> (pg_dump custom
/// format), <c>media/&lt;storageKey&gt;</c> per blob, and <c>missing-media.json</c> (keys whose blob was gone) only when
/// some were. The manifest can't list the missing ones: it's written before the blobs are read.
/// </summary>
public static partial class BackupArchive
{
	public const string Format = "cms-backup";
	public const int Version = 1;
	public const string ManifestEntry = "manifest.json";
	public const string DumpEntry = "db.dump";
	public const string MediaPrefix = "media/";
	public const string MissingMediaEntry = "missing-media.json";
	public const string Extension = ".tar.gz";

	public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

	[GeneratedRegex(@"^(auto|manual|pre-restore|upload)-\d{8}-\d{6}(-\d{1,3})?\.tar\.gz$")]
	public static partial Regex NamePattern();

	public static BackupKind KindOf(string name) => name.Split('-')[0] switch
	{
		"auto" => BackupKind.Auto,
		"manual" => BackupKind.Manual,
		"pre" => BackupKind.PreRestore,
		_ => BackupKind.Upload,
	};

	public static string Prefix(BackupKind kind) => kind switch
	{
		BackupKind.Auto => "auto",
		BackupKind.Manual => "manual",
		BackupKind.PreRestore => "pre-restore",
		_ => "upload",
	};

	public static async Task<BackupManifest> ReadManifestAsync(string path, CancellationToken ct)
	{
		await using var file = File.OpenRead(path);
		await using var gzip = new GZipStream(file, CompressionMode.Decompress);
		await using var tar = new TarReader(gzip);
		var entry = await tar.GetNextEntryAsync(cancellationToken: ct);
		return entry?.Name == ManifestEntry && entry.DataStream is not null
			? await ParseManifestAsync(entry.DataStream, ct)
			: throw new BackupFailedException("Not a CMS backup: the archive doesn't start with a manifest.");
	}

	/// <summary>
	/// Reads the whole archive (so gzip/tar damage shows up), checks its structure and returns the manifest.
	/// <paramref name="dumpTarget"/>: where to extract <c>db.dump</c>; <paramref name="onMedia"/>: called with each blob.
	/// </summary>
	public static async Task<BackupManifest> ScanAsync(
		string path, string? dumpTarget, Func<string, Stream, Task>? onMedia, CancellationToken ct)
	{
		BackupManifest? manifest = null;
		var hasDump = false;
		try
		{
			await using var file = File.OpenRead(path);
			await using var gzip = new GZipStream(file, CompressionMode.Decompress);
			await using var tar = new TarReader(gzip);
			while (await tar.GetNextEntryAsync(cancellationToken: ct) is { } entry)
			{
				if (manifest is null)
				{
					if (entry.Name != ManifestEntry || entry.DataStream is null)
						throw new BackupFailedException("Not a CMS backup: the archive doesn't start with a manifest.");
					manifest = await ParseManifestAsync(entry.DataStream, ct);
				}
				else if (entry.Name == DumpEntry && entry.DataStream is not null)
				{
					hasDump = true;
					if (dumpTarget is not null)
					{
						await using var target = File.Create(dumpTarget);
						await entry.DataStream.CopyToAsync(target, ct);
					}
				}
				else if (entry.Name.StartsWith(MediaPrefix, StringComparison.Ordinal) && entry.DataStream is not null)
				{
					var key = entry.Name[MediaPrefix.Length..];
					if (!MediaService.StorageKeyPattern().IsMatch(key))
						throw new BackupFailedException($"Unexpected media file '{entry.Name}' in the backup.");
					if (onMedia is not null)
						await onMedia(key, entry.DataStream);
				}
			}
		}
		catch (Exception e) when (e is InvalidDataException or EndOfStreamException or FormatException)
		{
			throw new BackupFailedException($"The backup file is damaged ({e.Message}).", e);
		}
		if (manifest is null)
			throw new BackupFailedException("Not a CMS backup: the archive is empty.");
		if (!hasDump)
			throw new BackupFailedException("The backup has no database dump.");
		return manifest;
	}

	private static async Task<BackupManifest> ParseManifestAsync(Stream data, CancellationToken ct)
	{
		BackupManifest? manifest;
		try
		{
			manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(data, Json, ct);
		}
		catch (JsonException e)
		{
			throw new BackupFailedException("The backup manifest is unreadable.", e);
		}
		if (manifest?.Format != Format)
			throw new BackupFailedException("Not a CMS backup (unknown manifest format).");
		if (manifest.Version > Version)
			throw new BackupFailedException("The backup was made by a newer CMS version.");
		return manifest;
	}
}
