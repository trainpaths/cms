using System.Text.Json.Serialization;
using api_backend.Models.Auth;

namespace api_backend.Models.Pages;

// lowercase on the wire and in OpenAPI (string union in the generated TS client)
[JsonConverter(typeof(JsonStringEnumConverter<PageStatus>))]
public enum PageStatus
{
	[JsonStringEnumMemberName("draft")] Draft,
	[JsonStringEnumMemberName("published")] Published,
}

/// <summary>
/// A CMS page: metadata in columns, the block tree in a single <c>jsonb</c> column.
/// Two copies of the content (title, meta, blocks): the working copy the editor saves, and the <c>Published*</c>
/// snapshot that publishing copies it into. The public site only ever reads the snapshot, at <c>/{Slug}</c> (slug and
/// tags have no snapshot: they are live).
/// </summary>
public class Page
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public required string Title { get; set; }
	public required string Slug { get; set; }
	public PageStatus Status { get; set; } = PageStatus.Draft;

	/// <summary>Browser-tab / search title, shown as "{MetaTitle} - {firm name}"; empty = the firm name alone.</summary>
	public string MetaTitle { get; set; } = "";
	/// <summary>Search / link-preview description; empty = the first paragraph.</summary>
	public string MetaDescription { get; set; } = "";

	public List<Block> Blocks { get; set; } = new();
	/// <summary>Title/meta per language code (the columns above are the default language's).</summary>
	public Dictionary<string, PageTextTranslation> Translations { get; set; } = new();
	/// <summary>Blocks at all depths; kept in sync with <see cref="Blocks"/> so lists don't read the tree.</summary>
	public int BlockCount { get; set; }
	public List<PageTag> PageTags { get; set; } = new();

	/// <summary>Live copy of <see cref="Title"/>; null = never published. Kept on unpublish.</summary>
	public string? PublishedTitle { get; set; }
	public string PublishedMetaTitle { get; set; } = "";
	public string PublishedMetaDescription { get; set; } = "";
	public List<Block>? PublishedBlocks { get; set; }
	public Dictionary<string, PageTextTranslation> PublishedTranslations { get; set; } = new();
	/// <summary>Published and the working copy differs from the snapshot; kept on every write so lists don't compare trees.</summary>
	public bool HasUnpublishedChanges { get; set; }

	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
	public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
	public DateTimeOffset? PublishedAt { get; set; }

	public Guid? CreatedById { get; set; }
	public Staff? CreatedBy { get; set; }
	public Guid? UpdatedById { get; set; }
	public Staff? UpdatedBy { get; set; }
}

/// <summary>A page's title and meta in one language; empty = untranslated (the default language's value applies).</summary>
public record PageTextTranslation(string Title = "", string MetaTitle = "", string MetaDescription = "");
