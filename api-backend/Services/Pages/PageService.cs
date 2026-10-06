using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using api_backend.Models.Dto;
using api_backend.Models.Pages;
using api_backend.Services.Cms;
using api_backend.Services.Media;
using Microsoft.EntityFrameworkCore;

namespace api_backend.Services.Pages;

/// <summary>Why a page operation failed; the controller maps it to a ProblemDetails status.</summary>
public enum PageError
{
	NotFound,
	SlugTaken,
	Invalid,
}

public record PageResult<T>(T? Value, PageError? Error = null, string? Detail = null)
{
	public static PageResult<T> Ok(T value) => new(value);
	public static PageResult<T> Fail(PageError error, string? detail = null) => new(default, error, detail);
}

/// <summary>
/// Page CRUD, publish, slug rules, block validation. Pages declared in the instance config (<see cref="CmsConfig"/>) can be
/// locked (fixed slug, undeletable) and template-rendered; those flags are looked up by slug, not stored.
/// </summary>
public partial class PageService(AppDbContext db, CmsConfig cms)
{
	public const int MaxSlugLength = 100;
	public const int MaxDepth = 10;
	public const int MaxBlocks = 2000;
	public const int MaxMetaTitleLength = 70;
	public const int MaxMetaDescriptionLength = 200;

	/// <summary>
	/// Slugs the public <c>/:slug</c> route can't use because the frontend or API owns the path.
	/// Keep in sync with the static routes in <c>frontend/src/router/index.ts</c>.
	/// </summary>
	public static readonly HashSet<string> ReservedSlugs =
	[
		"login", "register", "profile", "change-password", "forgot-password", "reset-password",
		"verify-email", "dashboard", "admin", "api", "health", "assets", "_diagnostics",
	];

	[GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
	private static partial Regex SlugPattern();

	[GeneratedRegex("^[a-z0-9-]+$")]
	private static partial Regex BlockNamePattern();

	[GeneratedRegex("[^a-z0-9]+")]
	private static partial Regex NonSlugChars();

	public async Task<List<PageSummary>> ListAsync(CancellationToken ct)
	{
		// Blocks is a jsonb value-converted column, so the block count is computed client-side.
		var pages = await db.Pages.AsNoTracking()
			.Include(p => p.PageTags).ThenInclude(pt => pt.Tag)
			.OrderByDescending(p => p.UpdatedAt)
			.ToListAsync(ct);
		return pages.Select(ToSummary).ToList();
	}

	public async Task<PageDetail?> GetAsync(Guid id, CancellationToken ct)
	{
		var page = await db.Pages.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
		return page is null ? null : await ToDetailAsync(page, ct);
	}

	public async Task<PublicPage?> GetPublishedBySlugAsync(string slug, CancellationToken ct)
	{
		var normalized = slug.ToLowerInvariant();
		var page = await db.Pages.AsNoTracking()
			.FirstOrDefaultAsync(p => p.Slug == normalized && p.Status == PageStatus.Published, ct);
		return page is null
			? null
			: new PublicPage(page.Title, page.Slug, page.Blocks,
				await MediaService.ResolveRefsAsync(db, page.Blocks, ct), page.PublishedAt, cms.Page(page.Slug)?.Template,
				page.MetaTitle, page.MetaDescription);
	}

	public async Task<PageResult<PageDetail>> CreateAsync(CreatePageRequest req, Guid staffId, CancellationToken ct)
	{
		var title = req.Title.Trim();
		if (title.Length == 0)
			return PageResult<PageDetail>.Fail(PageError.Invalid, "Title is required.");

		var blocks = req.Blocks ?? [];
		if (ValidateBlocks(blocks) is { } blockError)
			return PageResult<PageDetail>.Fail(PageError.Invalid, blockError);

		string slug;
		if (string.IsNullOrWhiteSpace(req.Slug))
		{
			slug = await UniqueSlugFromAsync(title, ct);
		}
		else
		{
			slug = req.Slug.Trim().ToLowerInvariant();
			if (ValidateSlug(slug) is { } slugError)
				return PageResult<PageDetail>.Fail(PageError.Invalid, slugError);
			if (await db.Pages.AnyAsync(p => p.Slug == slug, ct))
				return PageResult<PageDetail>.Fail(PageError.SlugTaken, $"Slug '{slug}' is already in use.");
		}

		var page = new Page
		{
			Title = title,
			Slug = slug,
			Blocks = blocks,
			CreatedById = staffId,
			UpdatedById = staffId,
		};
		db.Pages.Add(page);
		await db.SaveChangesAsync(ct);
		return PageResult<PageDetail>.Ok(await ToDetailAsync(page, ct));
	}

	/// <summary>
	/// Creates a draft page from an exported page file. Never overwrites: a taken (or reserved) slug gets a
	/// <c>-2</c>, <c>-3</c>... suffix. Media is referenced by id; ids unknown to this site are reported, not rejected.
	/// </summary>
	public async Task<PageResult<ImportPageResult>> ImportAsync(ImportPageRequest req, Guid staffId, CancellationToken ct)
	{
		var title = req.Title.Trim();
		if (title.Length == 0)
			return PageResult<ImportPageResult>.Fail(PageError.Invalid, "Title is required.");
		if ((MetaError(req.MetaTitle, MaxMetaTitleLength, "Meta title")
			?? MetaError(req.MetaDescription, MaxMetaDescriptionLength, "Meta description")) is { } metaError)
			return PageResult<ImportPageResult>.Fail(PageError.Invalid, metaError);
		if (ValidateBlocks(req.Blocks) is { } blockError)
			return PageResult<ImportPageResult>.Fail(PageError.Invalid, blockError);
		var (tagNames, tagError) = NormalizeTags(req.Tags ?? []);
		if (tagError is not null)
			return PageResult<ImportPageResult>.Fail(PageError.Invalid, tagError);

		var page = new Page
		{
			Title = title,
			Slug = await UniqueSlugFromAsync(string.IsNullOrWhiteSpace(req.Slug) ? title : req.Slug, ct),
			Blocks = req.Blocks,
			MetaTitle = req.MetaTitle?.Trim() ?? "",
			MetaDescription = req.MetaDescription?.Trim() ?? "",
			CreatedById = staffId,
			UpdatedById = staffId,
		};
		db.Pages.Add(page);
		await AddTagsAsync(page, tagNames, ct);
		await db.SaveChangesAsync(ct);

		var detail = await ToDetailAsync(page, ct);
		var found = detail.Media.Select(m => m.Id).ToHashSet();
		var missing = MediaService.CollectIds(page.Blocks).Where(id => !found.Contains(id)).ToList();
		return PageResult<ImportPageResult>.Ok(new ImportPageResult(detail, missing));
	}

	public async Task<PageResult<PageDetail>> UpdateAsync(Guid id, UpdatePageRequest req, Guid staffId, CancellationToken ct)
	{
		var page = await db.Pages.FirstOrDefaultAsync(p => p.Id == id, ct);
		if (page is null)
			return PageResult<PageDetail>.Fail(PageError.NotFound);
		var config = cms.Page(page.Slug);

		if (req.Title is not null)
		{
			var title = req.Title.Trim();
			if (title.Length == 0)
				return PageResult<PageDetail>.Fail(PageError.Invalid, "Title cannot be empty.");
			page.Title = title;
		}

		if (req.Slug is not null)
		{
			var slug = req.Slug.Trim().ToLowerInvariant();
			if (ValidateSlug(slug) is { } slugError)
				return PageResult<PageDetail>.Fail(PageError.Invalid, slugError);
			if (slug != page.Slug && config is { IsLocked: true })
				return PageResult<PageDetail>.Fail(PageError.Invalid, "This page's slug is fixed by the site configuration.");
			if (slug != page.Slug && await db.Pages.AnyAsync(p => p.Slug == slug, ct))
				return PageResult<PageDetail>.Fail(PageError.SlugTaken, $"Slug '{slug}' is already in use.");
			page.Slug = slug;
		}

		if ((MetaError(req.MetaTitle, MaxMetaTitleLength, "Meta title")
			?? MetaError(req.MetaDescription, MaxMetaDescriptionLength, "Meta description")) is { } metaError)
			return PageResult<PageDetail>.Fail(PageError.Invalid, metaError);
		if (req.MetaTitle is not null)
			page.MetaTitle = req.MetaTitle.Trim();
		if (req.MetaDescription is not null)
			page.MetaDescription = req.MetaDescription.Trim();

		if (req.Blocks is not null)
		{
			if (config is { Editable: false })
				return PageResult<PageDetail>.Fail(PageError.Invalid, "This page's content comes from its template.");
			if (ValidateBlocks(req.Blocks) is { } blockError)
				return PageResult<PageDetail>.Fail(PageError.Invalid, blockError);
			page.Blocks = req.Blocks;
		}

		page.UpdatedAt = DateTimeOffset.UtcNow;
		page.UpdatedById = staffId;
		await db.SaveChangesAsync(ct);
		return PageResult<PageDetail>.Ok(await ToDetailAsync(page, ct));
	}

	public async Task<PageResult<PageDetail>> SetStatusAsync(Guid id, PageStatus status, Guid staffId, CancellationToken ct)
	{
		var page = await db.Pages.FirstOrDefaultAsync(p => p.Id == id, ct);
		if (page is null)
			return PageResult<PageDetail>.Fail(PageError.NotFound);

		if (page.Status != status)
		{
			page.Status = status;
			page.PublishedAt = status == PageStatus.Published ? DateTimeOffset.UtcNow : null;
			page.UpdatedAt = DateTimeOffset.UtcNow;
			page.UpdatedById = staffId;
			await db.SaveChangesAsync(ct);
		}
		return PageResult<PageDetail>.Ok(await ToDetailAsync(page, ct));
	}

	// null = not sent (unchanged); empty clears
	private static string? MetaError(string? value, int max, string label) =>
		value?.Trim().Length > max ? $"{label} must be at most {max} characters." : null;

	public async Task<PageResult<bool>> DeleteAsync(Guid id, CancellationToken ct)
	{
		var page = await db.Pages.Include(p => p.PageTags).FirstOrDefaultAsync(p => p.Id == id, ct);
		if (page is null)
			return PageResult<bool>.Fail(PageError.NotFound);
		if (cms.Page(page.Slug) is { IsLocked: true })
			return PageResult<bool>.Fail(PageError.Invalid, "This page is part of the site configuration and can't be deleted.");
		db.Pages.Remove(page);
		await db.SaveChangesAsync(ct);
		await DeleteUnusedTagsAsync(ct);
		return PageResult<bool>.Ok(true);
	}

	/// <summary>
	/// Replaces the page's tags. Doesn't touch <c>UpdatedAt</c>: tagging isn't a content edit, and the
	/// pages list sorts by it.
	/// </summary>
	public async Task<PageResult<PageDetail>> SetTagsAsync(Guid id, List<string> raw, CancellationToken ct)
	{
		var page = await db.Pages.Include(p => p.PageTags).FirstOrDefaultAsync(p => p.Id == id, ct);
		if (page is null)
			return PageResult<PageDetail>.Fail(PageError.NotFound);

		var (names, error) = NormalizeTags(raw);
		if (error is not null)
			return PageResult<PageDetail>.Fail(PageError.Invalid, error);

		var tags = await LoadOrCreateTagsAsync(names, ct);
		db.PageTags.RemoveRange(page.PageTags.Where(pt => tags.All(t => t.Id != pt.TagId)));
		foreach (var tag in tags.Where(t => page.PageTags.All(pt => pt.TagId != t.Id)))
			page.PageTags.Add(new PageTag { Page = page, Tag = tag });
		await db.SaveChangesAsync(ct);
		await DeleteUnusedTagsAsync(ct);
		return PageResult<PageDetail>.Ok(await ToDetailAsync(page, ct));
	}

	/// <summary>Normalized, deduplicated tag names, or the first validation error.</summary>
	private static (List<string> Names, string? Error) NormalizeTags(List<string> raw)
	{
		var names = new List<string>();
		foreach (var value in raw)
		{
			var name = Tag.Normalize(value ?? "");
			if (name.Length is 0 or > TagLimits.MaxLength)
				return (names, $"Tags must be 1-{TagLimits.MaxLength} characters.");
			if (name.Any(char.IsWhiteSpace))
				return (names, $"Tags are single words: \"{name}\" contains a space.");
			if (!names.Contains(name))
				names.Add(name);
		}
		return names.Count > TagLimits.MaxPerPage
			? (names, $"A page may have at most {TagLimits.MaxPerPage} tags.")
			: (names, null);
	}

	// new tags are only tracked; the caller saves
	private async Task<List<Tag>> LoadOrCreateTagsAsync(List<string> names, CancellationToken ct)
	{
		var tags = await db.Tags.Where(t => names.Contains(t.Name)).ToListAsync(ct);
		foreach (var name in names.Where(n => tags.All(t => t.Name != n)))
		{
			var tag = new Tag { Name = name };
			db.Tags.Add(tag);
			tags.Add(tag);
		}
		return tags;
	}

	private async Task AddTagsAsync(Page page, List<string> names, CancellationToken ct)
	{
		foreach (var tag in await LoadOrCreateTagsAsync(names, ct))
			page.PageTags.Add(new PageTag { Page = page, Tag = tag });
	}

	/// <summary>Tags in use, most used first (autocomplete + "popular" suggestions).</summary>
	public async Task<List<TagUsage>> ListTagsAsync(CancellationToken ct) =>
		await db.Tags.AsNoTracking()
			.Where(t => t.PageTags.Any())
			.OrderByDescending(t => t.PageTags.Count).ThenBy(t => t.Name)
			.Select(t => new TagUsage(t.Name, t.PageTags.Count))
			.ToListAsync(ct);

	// autocomplete only offers tags that are still on some page
	private async Task DeleteUnusedTagsAsync(CancellationToken ct)
	{
		var unused = await db.Tags.Where(t => !t.PageTags.Any()).ToListAsync(ct);
		if (unused.Count == 0)
			return;
		db.Tags.RemoveRange(unused);
		await db.SaveChangesAsync(ct);
	}

	public static string? ValidateSlug(string slug)
	{
		if (slug.Length is 0 or > MaxSlugLength)
			return $"Slug must be 1-{MaxSlugLength} characters.";
		if (!SlugPattern().IsMatch(slug))
			return "Slug may only contain lowercase letters, digits and single hyphens.";
		if (ReservedSlugs.Contains(slug))
			return $"Slug '{slug}' is reserved.";
		return null;
	}

	/// <summary>
	/// Returns an error message for the first invalid block, or null when the tree is valid.
	/// Also normalizes explicit JSON nulls for <c>attributes</c>/<c>innerBlocks</c> to empty collections.
	/// </summary>
	public static string? ValidateBlocks(List<Block> blocks)
	{
		var count = 0;
		return Walk(blocks, 1);

		string? Walk(List<Block> level, int depth)
		{
			if (depth > MaxDepth)
				return $"Blocks may be nested at most {MaxDepth} levels deep.";
			foreach (var block in level)
			{
				if (++count > MaxBlocks)
					return $"A page may contain at most {MaxBlocks} blocks.";
				if (string.IsNullOrWhiteSpace(block.Id) || block.Id.Length > 64)
					return "Every block needs an id of at most 64 characters.";
				if (string.IsNullOrEmpty(block.Name) || block.Name.Length > 64 || !BlockNamePattern().IsMatch(block.Name))
					return $"Invalid block name '{block.Name}'.";
				block.Attributes ??= new();
				block.InnerBlocks ??= [];
				foreach (var (key, value) in block.Attributes)
				{
					if (value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False))
						return $"Attribute '{key}' of block '{block.Id}' must be a string, number or boolean.";
				}
				if (Walk(block.InnerBlocks, depth + 1) is { } error)
					return error;
			}
			return null;
		}
	}

	/// <summary>"Hello Wörld!" → "hello-world".</summary>
	public static string Slugify(string text)
	{
		var decomposed = text.Normalize(NormalizationForm.FormD);
		var sb = new StringBuilder(decomposed.Length);
		foreach (var c in decomposed)
		{
			if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
				sb.Append(c);
		}
		var slug = NonSlugChars().Replace(sb.ToString().ToLowerInvariant(), "-").Trim('-');
		if (slug.Length > MaxSlugLength - 4)
			slug = slug[..(MaxSlugLength - 4)].TrimEnd('-');
		return slug.Length == 0 ? "page" : slug;
	}

	private async Task<string> UniqueSlugFromAsync(string title, CancellationToken ct)
	{
		var baseSlug = Slugify(title);
		if (ReservedSlugs.Contains(baseSlug))
			baseSlug += "-page";

		var taken = await db.Pages
			.Where(p => p.Slug == baseSlug || p.Slug.StartsWith(baseSlug + "-"))
			.Select(p => p.Slug)
			.ToListAsync(ct);
		if (!taken.Contains(baseSlug))
			return baseSlug;
		for (var i = 2; ; i++)
		{
			var candidate = $"{baseSlug}-{i}";
			if (!taken.Contains(candidate))
				return candidate;
		}
	}

	private static int CountBlocks(List<Block> blocks) =>
		blocks.Sum(b => 1 + CountBlocks(b.InnerBlocks ?? []));

	private PageSummary ToSummary(Page p) =>
		new(p.Id, p.Title, p.Slug, p.Status, p.PageTags.Select(pt => pt.Tag.Name).Order().ToList(),
			CountBlocks(p.Blocks), p.CreatedAt, p.UpdatedAt, p.PublishedAt, cms.Page(p.Slug) is { IsLocked: true });

	// tags queried, not taken from p.PageTags: callers load the page without them
	private async Task<PageDetail> ToDetailAsync(Page p, CancellationToken ct)
	{
		var config = cms.Page(p.Slug);
		return new(p.Id, p.Title, p.Slug, p.Status,
			await db.PageTags.Where(pt => pt.PageId == p.Id).Select(pt => pt.Tag.Name).OrderBy(n => n).ToListAsync(ct),
			p.Blocks, await MediaService.ResolveRefsAsync(db, p.Blocks, ct), p.CreatedAt, p.UpdatedAt, p.PublishedAt,
			config?.Template, config?.IsLocked ?? false, config?.Editable ?? true, p.MetaTitle, p.MetaDescription);
	}
}
