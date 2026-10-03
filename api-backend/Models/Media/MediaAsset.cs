using api_backend.Models.Auth;

namespace api_backend.Models.Media;

/// <summary>
/// An uploaded file (images only for now). Metadata lives here, the bytes in blob storage under
/// <see cref="StorageKey"/>. Blocks reference it by <see cref="Id"/> (<c>mediaId</c> attribute), so the
/// alt text is maintained once per file, not per block.
/// </summary>
public class MediaAsset
{
	public Guid Id { get; set; } = Guid.NewGuid();

	/// <summary><c>{Id:N}.{ext}</c>; also the public file name in <c>/api/public/media/{key}</c>.</summary>
	public required string StorageKey { get; set; }

	/// <summary>Original upload name, display only.</summary>
	public required string FileName { get; set; }
	public required string ContentType { get; set; }
	public long Size { get; set; }
	public string Alt { get; set; } = "";

	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
	public Guid? CreatedById { get; set; }
	public Staff? CreatedBy { get; set; }
}
