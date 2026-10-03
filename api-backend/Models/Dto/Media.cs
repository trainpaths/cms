using System.ComponentModel.DataAnnotations;

namespace api_backend.Models.Dto;

/// <summary>A media library entry. <see cref="Url"/> is same-origin (<c>/api/public/media/{key}</c>).</summary>
public record MediaItem(
	Guid Id,
	string Url,
	string FileName,
	string ContentType,
	long Size,
	string Alt,
	DateTimeOffset CreatedAt);

/// <summary>What a page needs to render a referenced media asset.</summary>
public record MediaRef(Guid Id, string Url, string Alt);

/// <summary>Null <see cref="FileName"/> keeps the current name; it's display only, the URL never changes.</summary>
public record UpdateMediaRequest(
	[MaxLength(MediaLimits.MaxAltLength)] string? Alt,
	[MaxLength(MediaLimits.MaxFileNameLength)] string? FileName = null);

public static class MediaLimits
{
	public const int MaxAltLength = 100;
	public const int MaxFileNameLength = 255;
}
