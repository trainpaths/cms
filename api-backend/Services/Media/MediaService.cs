using System.Text.Json;
using System.Text.RegularExpressions;
using api_backend.Models.Dto;
using api_backend.Models.Media;
using api_backend.Models.Pages;
using Microsoft.EntityFrameworkCore;

namespace api_backend.Services.Media;

public enum MediaError
{
	NotFound,
	Invalid,
}

public record MediaResult<T>(T? Value, MediaError? Error = null, string? Detail = null)
{
	public static MediaResult<T> Ok(T value) => new(value);
	public static MediaResult<T> Fail(MediaError error, string? detail = null) => new(default, error, detail);
}

/// <summary>
/// Media library: uploads (validated by magic bytes, never by the client's Content-Type), alt text,
/// deletion, and resolving the <c>mediaId</c> attributes of a block tree for page responses.
/// </summary>
public partial class MediaService(AppDbContext db, IBlobStorage blobs, ILogger<MediaService> logger)
{
	public const long MaxUploadBytes = 10 * 1024 * 1024;
	public const string PublicUrlPrefix = "/api/public/media/";

	/// <summary>Block attribute that references a media asset (image and card blocks).</summary>
	public const string MediaIdAttribute = "mediaId";

	// Raster formats only: SVG can carry script and is served same-origin.
	private static readonly (string ContentType, string Ext, Func<byte[], bool> Matches)[] Formats =
	[
		("image/jpeg", "jpg", b => b is [0xFF, 0xD8, 0xFF, ..]),
		("image/png", "png", b => b is [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..]),
		("image/gif", "gif", b => b is [0x47, 0x49, 0x46, 0x38, 0x37 or 0x39, 0x61, ..]),
		("image/webp", "webp", b => b.Length >= 12 && b[..4].SequenceEqual("RIFF"u8.ToArray()) && b[8..12].SequenceEqual("WEBP"u8.ToArray())),
		("image/avif", "avif", b => b.Length >= 12 && b[4..8].SequenceEqual("ftyp"u8.ToArray())
			&& (b[8..12].SequenceEqual("avif"u8.ToArray()) || b[8..12].SequenceEqual("avis"u8.ToArray()))),
	];

	public static readonly string[] AllowedContentTypes = Formats.Select(f => f.ContentType).ToArray();

	[GeneratedRegex("^[0-9a-f]{32}\\.(jpg|png|gif|webp|avif)$")]
	public static partial Regex StorageKeyPattern();

	public static string PublicUrl(string storageKey) => PublicUrlPrefix + storageKey;

	/// <summary>The asset as blocks and the site config reference it (public URL + alt).</summary>
	public static MediaRef ToRef(MediaAsset asset) => new(asset.Id, PublicUrl(asset.StorageKey), asset.Alt);

	public async Task<List<MediaItem>> ListAsync(CancellationToken ct)
	{
		var assets = await db.MediaAssets.AsNoTracking().OrderByDescending(m => m.CreatedAt).ToListAsync(ct);
		return assets.Select(ToItem).ToList();
	}

	public async Task<MediaItem?> GetAsync(Guid id, CancellationToken ct)
	{
		var asset = await db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
		return asset is null ? null : ToItem(asset);
	}

	public async Task<MediaResult<MediaItem>> UploadAsync(IFormFile file, string? alt, Guid staffId, CancellationToken ct)
	{
		if (file.Length == 0)
			return MediaResult<MediaItem>.Fail(MediaError.Invalid, "File is empty.");
		if (file.Length > MaxUploadBytes)
			return MediaResult<MediaItem>.Fail(MediaError.Invalid, $"File exceeds {MaxUploadBytes / 1024 / 1024} MB.");
		if (alt is { Length: > MediaLimits.MaxAltLength })
			return MediaResult<MediaItem>.Fail(MediaError.Invalid, $"Alt text exceeds {MediaLimits.MaxAltLength} characters.");

		// ≤10 MB, buffered: needed for sniffing anyway and gives S3 a seekable stream with a length
		using var buffer = new MemoryStream((int)file.Length);
		await file.CopyToAsync(buffer, ct);
		var bytes = buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 16)).ToArray();

		var format = Formats.FirstOrDefault(f => f.Matches(bytes));
		if (format.ContentType is null)
			return MediaResult<MediaItem>.Fail(MediaError.Invalid,
				"Unsupported file type. Allowed: JPEG, PNG, GIF, WebP, AVIF.");

		var asset = new MediaAsset
		{
			StorageKey = "",
			FileName = SanitizeFileName(file.FileName),
			ContentType = format.ContentType,
			Size = buffer.Length,
			Alt = alt?.Trim() ?? "",
			CreatedById = staffId,
		};
		asset.StorageKey = $"{asset.Id:N}.{format.Ext}";

		buffer.Position = 0;
		await blobs.PutAsync(asset.StorageKey, buffer, asset.ContentType, ct);

		db.MediaAssets.Add(asset);
		try
		{
			await db.SaveChangesAsync(ct);
		}
		catch
		{
			await TryDeleteBlobAsync(asset.StorageKey);
			throw;
		}
		return MediaResult<MediaItem>.Ok(ToItem(asset));
	}

	public async Task<MediaResult<MediaItem>> UpdateAsync(Guid id, UpdateMediaRequest req, CancellationToken ct)
	{
		var asset = await db.MediaAssets.FirstOrDefaultAsync(m => m.Id == id, ct);
		if (asset is null)
			return MediaResult<MediaItem>.Fail(MediaError.NotFound);

		if (req.FileName is not null)
		{
			var name = new string(req.FileName.Where(c => !char.IsControl(c)).ToArray()).Trim();
			if (name.Length == 0)
				return MediaResult<MediaItem>.Fail(MediaError.Invalid, "File name must not be empty.");
			asset.FileName = name;
		}
		asset.Alt = req.Alt?.Trim() ?? "";
		await db.SaveChangesAsync(ct);
		return MediaResult<MediaItem>.Ok(ToItem(asset));
	}

	/// <summary>Deletes the blob, then the row: a failed blob delete leaves the entry for a retry.</summary>
	public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
	{
		var asset = await db.MediaAssets.FirstOrDefaultAsync(m => m.Id == id, ct);
		if (asset is null)
			return false;
		await blobs.DeleteAsync(asset.StorageKey, ct);
		db.MediaAssets.Remove(asset);
		await db.SaveChangesAsync(ct);
		return true;
	}

	/// <summary>Null for malformed keys, so arbitrary bucket paths are never requested.</summary>
	public async Task<BlobObject?> OpenPublicAsync(string storageKey, CancellationToken ct) =>
		StorageKeyPattern().IsMatch(storageKey) ? await blobs.GetAsync(storageKey, ct) : null;

	/// <summary>
	/// Resolves every <c>mediaId</c> attribute in the tree to a <see cref="MediaRef"/>.
	/// Unknown or deleted ids are simply absent; the frontend renders nothing for them.
	/// </summary>
	public static async Task<List<MediaRef>> ResolveRefsAsync(AppDbContext db, List<Block> blocks, CancellationToken ct)
	{
		var ids = new HashSet<Guid>();
		Collect(blocks);
		if (ids.Count == 0)
			return [];

		var assets = await db.MediaAssets.AsNoTracking().Where(m => ids.Contains(m.Id)).ToListAsync(ct);
		return assets.Select(ToRef).ToList();

		void Collect(List<Block> level)
		{
			foreach (var block in level)
			{
				if (block.Attributes?.TryGetValue(MediaIdAttribute, out var value) == true
					&& value.ValueKind == JsonValueKind.String
					&& Guid.TryParse(value.GetString(), out var id))
					ids.Add(id);
				Collect(block.InnerBlocks ?? []);
			}
		}
	}

	private async Task TryDeleteBlobAsync(string key)
	{
		try
		{
			await blobs.DeleteAsync(key, CancellationToken.None);
		}
		catch (Exception e)
		{
			logger.LogWarning(e, "Orphaned blob {Key} after failed media insert", key);
		}
	}

	private static string SanitizeFileName(string name)
	{
		var clean = new string(Path.GetFileName(name).Where(c => !char.IsControl(c)).ToArray()).Trim();
		if (clean.Length == 0)
			return "upload";
		return clean.Length > MediaLimits.MaxFileNameLength ? clean[..MediaLimits.MaxFileNameLength] : clean;
	}

	private static MediaItem ToItem(MediaAsset a) =>
		new(a.Id, PublicUrl(a.StorageKey), a.FileName, a.ContentType, a.Size, a.Alt, a.CreatedAt);
}
