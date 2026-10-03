namespace api_backend.Models.Pages;

/// <summary>
/// A page tag. <see cref="Name"/> is always stored normalized (<see cref="Normalize"/>), so lookups and the
/// unique index need no second column. Tags without pages are deleted (<c>PageService</c>).
/// </summary>
public class Tag
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public required string Name { get; set; }

	public List<PageTag> PageTags { get; set; } = new();

	/// <summary>" News " → "news". Tags are single words: whitespace inside is rejected, not joined. Mirrored by the frontend's <c>normalizeTag</c>.</summary>
	public static string Normalize(string raw) => raw.Trim().ToLowerInvariant();
}

public class PageTag
{
	public Guid PageId { get; set; }
	public Page Page { get; set; } = null!;
	public Guid TagId { get; set; }
	public Tag Tag { get; set; } = null!;
}

public static class TagLimits
{
	public const int MaxLength = 32;
	public const int MaxPerPage = 20;
}
