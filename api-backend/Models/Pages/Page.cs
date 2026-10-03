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
/// Published pages are publicly readable at <c>/{Slug}</c>.
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
	public List<PageTag> PageTags { get; set; } = new();

	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
	public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
	public DateTimeOffset? PublishedAt { get; set; }

	public Guid? CreatedById { get; set; }
	public Staff? CreatedBy { get; set; }
	public Guid? UpdatedById { get; set; }
	public Staff? UpdatedBy { get; set; }
}
