using System.ComponentModel.DataAnnotations;
using api_backend.Models.Pages;
using api_backend.Services.Pages;

// Media: the assets referenced by `mediaId` block attributes, so the page renders without extra requests.

namespace api_backend.Models.Dto;

public record PageSummary(
	Guid Id,
	string Title,
	string Slug,
	PageStatus Status,
	List<string> Tags,
	int BlockCount,
	DateTimeOffset CreatedAt,
	DateTimeOffset UpdatedAt,
	DateTimeOffset? PublishedAt,
	bool Locked);

/// <summary>
/// <see cref="Template"/>, <see cref="Locked"/> (slug fixed, undeletable) and <see cref="Editable"/> (blocks writable) come
/// from the instance config's entry for the slug.
/// </summary>
public record PageDetail(
	Guid Id,
	string Title,
	string Slug,
	PageStatus Status,
	List<string> Tags,
	List<Block> Blocks,
	List<MediaRef> Media,
	DateTimeOffset CreatedAt,
	DateTimeOffset UpdatedAt,
	DateTimeOffset? PublishedAt,
	string? Template,
	bool Locked,
	bool Editable,
	string MetaTitle,
	string MetaDescription);

/// <summary>Slug is generated from the title when omitted.</summary>
public record CreatePageRequest(
	[Required, MaxLength(200)] string Title,
	[MaxLength(100)] string? Slug,
	List<Block>? Blocks);

/// <summary>Partial update: only non-null fields are applied (empty meta strings clear them).</summary>
public record UpdatePageRequest(
	[MaxLength(200)] string? Title,
	[MaxLength(100)] string? Slug,
	List<Block>? Blocks,
	[MaxLength(PageService.MaxMetaTitleLength)] string? MetaTitle = null,
	[MaxLength(PageService.MaxMetaDescriptionLength)] string? MetaDescription = null);

/// <summary>Full replace of the page's tags; names are normalized (trimmed, lowercase) and deduplicated.</summary>
public record UpdatePageTagsRequest([MaxLength(TagLimits.MaxPerPage)] List<string> Tags);

/// <summary>A tag in use and how many pages carry it (autocomplete, "popular" suggestions).</summary>
public record TagUsage(string Name, int PageCount);

/// <summary><see cref="Template"/>: the instance template that renders the page instead of the default layout.</summary>
public record PublicPage(
	string Title,
	string Slug,
	List<Block> Blocks,
	List<MediaRef> Media,
	DateTimeOffset? PublishedAt,
	string? Template,
	string MetaTitle,
	string MetaDescription);
