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
/// What a write did to the public site. <see cref="StatusChanged"/>: published or unpublished.
/// <see cref="LiveChanged"/>: the live page changed while staying published (publish changes, slug).
/// <see cref="LiveRenamed"/>: its live title or slug changed (menus + footer links on every page show those).
/// </summary>
public record PageUpdate(PageSummary Page, bool StatusChanged, bool LiveChanged, bool LiveRenamed);

/// <summary>
/// Page CRUD, publish (working copy → live snapshot, <see cref="Page"/>), slug rules, block validation. Pages declared in the instance config (<see cref="CmsConfig"/>) can be
/// locked (fixed slug, undeletable) and template-rendered; those flags are looked up by slug, not stored.
/// </summary>
public partial class PageService(AppDbContext db, CmsConfig cms)
{
	public const int MaxTitleLength = 200;
	public const int MaxSlugLength = 100;
	public const int MaxDepth = 10;
	public const int MaxBlocks = 2000;
	public const int MaxMetaTitleLength = 70;
	public const int MaxMetaDescriptionLength = 200;

	/// <summary>
	/// Slugs the public <c>/:slug</c> route can't use because the frontend or API owns the path: the customer screens
	/// (<see cref="Rendering.PublicScreens"/>; <c>user</c> = the account pages under it), the admin SPA's routes
	/// (<c>frontend/src/router/index.ts</c>) and the API's. Keep in sync with both.
	/// </summary>
	public static readonly HashSet<string> ReservedSlugs =
	[
		"login", "register", "forgot-password", "reset-password", "verify-email", "user",
		"dashboard", "admin", "api", "health", "assets", "_diagnostics",
	];

	[GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
	private static partial Regex SlugPattern();

	[GeneratedRegex("^[a-z0-9-]+$")]
	private static partial Regex BlockNamePattern();

	[GeneratedRegex("[^a-z0-9]+")]
	private static partial Regex NonSlugChars();

	/// <summary>Language code (primary subtag): keys of block and page translations.</summary>
	[GeneratedRegex("^[a-z]{2,3}$")]
	private static partial Regex LangCodePattern();

	/// <summary>Longest translation source hash (the editor writes 8 hex digits).</summary>
	public const int MaxSourceHashLength = 16;

	public async Task<List<PageSummary>> ListAsync(CancellationToken ct)
	{
		// no Blocks (up to 2 MB each): the stored BlockCount instead
		var pages = await db.Pages.AsNoTracking()
			.OrderByDescending(p => p.UpdatedAt)
			.Select(p => new
			{
				p.Id, p.Title, p.Slug, p.Status, p.BlockCount, p.CreatedAt, p.UpdatedAt, p.PublishedAt,
				p.HasUnpublishedChanges,
				Tags = p.PageTags.Select(pt => pt.Tag.Name).OrderBy(n => n).ToList(),
			})
			.ToListAsync(ct);
		// Locked after materializing: cms.Page(slug) isn't SQL
		return pages.Select(p => new PageSummary(p.Id, p.Title, p.Slug, p.Status, p.Tags, p.BlockCount, p.CreatedAt,
			p.UpdatedAt, p.PublishedAt, p.HasUnpublishedChanges, cms.Page(p.Slug) is { IsLocked: true })).ToList();
	}

	public async Task<PageDetail?> GetAsync(Guid id, CancellationToken ct)
	{
		// tags as one string_agg column, not a second query (tags never contain whitespace). No published snapshot:
		// the editor works on the working copy (a second block tree of up to 2 MB)
		var row = await db.Pages.AsNoTracking()
			.Where(p => p.Id == id)
			.Select(p => new
			{
				Page = new Page
				{
					Id = p.Id,
					Title = p.Title,
					Slug = p.Slug,
					Status = p.Status,
					MetaTitle = p.MetaTitle,
					MetaDescription = p.MetaDescription,
					Blocks = p.Blocks,
					Translations = p.Translations,
					CreatedAt = p.CreatedAt,
					UpdatedAt = p.UpdatedAt,
					PublishedAt = p.PublishedAt,
					HasUnpublishedChanges = p.HasUnpublishedChanges,
				},
				Tags = string.Join(" ", p.PageTags.Select(pt => pt.Tag.Name)),
			})
			.FirstOrDefaultAsync(ct);
		return row is null ? null : await ToDetailAsync(row.Page, SplitTags(row.Tags), ct);
	}

	private static List<string> SplitTags(string? joined) =>
		(joined ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal).ToList();

	/// <summary>The live snapshot of a published page (never the working copy), all languages.</summary>
	public record LivePage(Guid Id, string Slug, string Title, string MetaTitle, string MetaDescription,
		List<Block> Blocks, Dictionary<string, PageTextTranslation> Translations, DateTimeOffset? PublishedAt);

	private IQueryable<LivePage> Live(IQueryable<Page> pages) => pages
		.Where(p => p.Status == PageStatus.Published)
		.Select(p => new LivePage(p.Id, p.Slug, p.PublishedTitle!, p.PublishedMetaTitle, p.PublishedMetaDescription,
			p.PublishedBlocks!, p.PublishedTranslations, p.PublishedAt));

	/// <param name="lang">language code (unknown/null → default)</param>
	public async Task<PublicPage?> GetPublishedBySlugAsync(string slug, string? lang, CancellationToken ct)
	{
		var normalized = slug.ToLowerInvariant();
		var page = await Live(db.Pages.AsNoTracking().Where(p => p.Slug == normalized)).FirstOrDefaultAsync(ct);
		return page is null
			? null
			: ToPublic(Localize(page, cms.Site.Lang.Resolve(lang), cms.Site.Lang.DefaultCode),
				await MediaService.ResolveRefsAsync(db, page.Blocks, ct));
	}

	/// <summary>
	/// Published pages among <paramref name="ids"/> (others absent), in each of <paramref name="codes"/>: two queries,
	/// media refs shared.
	/// </summary>
	public async Task<Dictionary<Guid, Dictionary<string, PublicPage>>> GetPublishedByIdsAsync(
		IReadOnlyCollection<Guid> ids, IReadOnlyList<string> codes, CancellationToken ct)
	{
		var list = await Live(db.Pages.AsNoTracking().Where(p => ids.Contains(p.Id))).ToListAsync(ct);
		var mediaIds = list.SelectMany(p => MediaService.CollectIds(p.Blocks)).ToHashSet();
		var media = mediaIds.Count == 0
			? new Dictionary<Guid, MediaRef>()
			: await db.MediaAssets.AsNoTracking()
				.Where(m => mediaIds.Contains(m.Id))
				.ToDictionaryAsync(m => m.Id, MediaService.ToRef, ct);
		var defaultCode = cms.Site.Lang.DefaultCode;
		return list.ToDictionary(p => p.Id, p =>
		{
			var refs = MediaService.CollectIds(p.Blocks).Where(media.ContainsKey).Select(id => media[id]).ToList();
			return codes.ToDictionary(code => code, code => ToPublic(Localize(p, code, defaultCode), refs));
		});
	}

	private PublicPage ToPublic(LivePage page, List<MediaRef> media) =>
		new(page.Title, page.Slug, page.Blocks, media, page.PublishedAt, cms.Page(page.Slug)?.Template,
			page.MetaTitle, page.MetaDescription);

	/// <summary>
	/// <paramref name="page"/> in language <paramref name="code"/>: block attributes overlaid with that language's
	/// translations (non-empty strings, any other primitive), title/meta from its page translation where non-empty, the
	/// default language's everywhere else. Every block's <c>translations</c> is removed (visitors never get other
	/// languages' text); the default (or an unknown) code just strips them.
	/// </summary>
	public static LivePage Localize(LivePage page, string code, string defaultCode)
	{
		var overlay = code != defaultCode;
		var text = overlay ? page.Translations.GetValueOrDefault(code) : null;
		static string Pick(string? translated, string fallback) =>
			string.IsNullOrEmpty(translated) ? fallback : translated;
		return page with
		{
			Title = Pick(text?.Title, page.Title),
			MetaTitle = Pick(text?.MetaTitle, page.MetaTitle),
			MetaDescription = Pick(text?.MetaDescription, page.MetaDescription),
			Blocks = Blocks(page.Blocks),
			Translations = [],
		};

		List<Block> Blocks(List<Block> blocks) => blocks.Select(b =>
		{
			var attributes = new Dictionary<string, JsonElement>(b.Attributes);
			if (overlay && b.Translations?.GetValueOrDefault(code) is { } values)
				foreach (var (key, value) in values)
					if (value.ValueKind != JsonValueKind.String || value.GetString()!.Length > 0)
						attributes[key] = value;
			return new Block { Id = b.Id, Name = b.Name, Attributes = attributes, InnerBlocks = Blocks(b.InnerBlocks) };
		}).ToList();
	}

	/// <summary>The live title in <paramref name="code"/> (menus, footer links): its translation, else the default.</summary>
	public static string LiveTitle(string title, Dictionary<string, PageTextTranslation> translations, string code,
		string defaultCode) =>
		code != defaultCode && translations.GetValueOrDefault(code)?.Title is { Length: > 0 } translated ? translated : title;

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
			if (SlugError(slug) is { } slugError)
				return PageResult<PageDetail>.Fail(PageError.Invalid, slugError);
			if (await db.Pages.AnyAsync(p => p.Slug == slug, ct))
				return PageResult<PageDetail>.Fail(PageError.SlugTaken, $"Slug '{slug}' is already in use.");
		}

		var page = new Page
		{
			Title = title,
			Slug = slug,
			Blocks = blocks,
			BlockCount = CountBlocks(blocks),
			CreatedById = staffId,
			UpdatedById = staffId,
		};
		db.Pages.Add(page);
		await db.SaveChangesAsync(ct);
		return PageResult<PageDetail>.Ok(await ToDetailAsync(page, [], ct));
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
		var (translations, translationError) = NormalizeTranslations(req.Translations ?? []);
		if (translationError is not null)
			return PageResult<ImportPageResult>.Fail(PageError.Invalid, translationError);

		var detail = await CreateDraftAsync(title, string.IsNullOrWhiteSpace(req.Slug) ? title : req.Slug, req.Blocks,
			req.MetaTitle?.Trim() ?? "", req.MetaDescription?.Trim() ?? "", translations, tagNames, staffId, ct);
		var found = detail.Media.Select(m => m.Id).ToHashSet();
		var missing = MediaService.CollectIds(req.Blocks).Where(id => !found.Contains(id)).ToList();
		return PageResult<ImportPageResult>.Ok(new ImportPageResult(detail, missing));
	}

	/// <summary>A draft copy: "{title} (copy)", a free variant of the slug, same blocks, meta, translations and tags.</summary>
	public async Task<PageResult<PageDetail>> DuplicateAsync(Guid id, Guid staffId, CancellationToken ct)
	{
		var source = await db.Pages.AsNoTracking()
			.Where(p => p.Id == id)
			.Select(p => new { Page = p, Tags = string.Join(" ", p.PageTags.Select(pt => pt.Tag.Name)) })
			.FirstOrDefaultAsync(ct);
		if (source is null)
			return PageResult<PageDetail>.Fail(PageError.NotFound);
		var page = source.Page;
		var title = $"{page.Title} (copy)";
		if (title.Length > MaxTitleLength)
			title = page.Title[..(MaxTitleLength - 7)].TrimEnd() + " (copy)";
		return PageResult<PageDetail>.Ok(await CreateDraftAsync(title, page.Slug, page.Blocks, page.MetaTitle,
			page.MetaDescription, page.Translations, SplitTags(source.Tags), staffId, ct));
	}

	/// <summary>Import + duplicate: inputs already validated; the slug is a free variant of <paramref name="slugSource"/>.</summary>
	private async Task<PageDetail> CreateDraftAsync(string title, string slugSource, List<Block> blocks, string metaTitle,
		string metaDescription, Dictionary<string, PageTextTranslation> translations, List<string> tagNames, Guid staffId,
		CancellationToken ct)
	{
		var page = new Page
		{
			Title = title,
			Slug = await UniqueSlugFromAsync(slugSource, ct),
			Blocks = blocks,
			Translations = translations,
			BlockCount = CountBlocks(blocks),
			MetaTitle = metaTitle,
			MetaDescription = metaDescription,
			CreatedById = staffId,
			UpdatedById = staffId,
		};
		db.Pages.Add(page);
		if (tagNames.Count > 0)
			await AddTagsAsync(page, tagNames, ct);
		await db.SaveChangesAsync(ct);
		return await ToDetailAsync(page, tagNames.Order(StringComparer.Ordinal).ToList(), ct);
	}

	/// <summary>
	/// Partial update of everything an editor writes (content, meta, slug, status, tags) in one SaveChanges. Everything
	/// is validated before anything changes. Content goes to the working copy only; status <c>published</c> then copies
	/// it to the live snapshot (also when already published), <c>draft</c> unpublishes (the snapshot stays). Tags alone
	/// don't bump <c>UpdatedAt</c>: tagging isn't a content edit, and the pages list sorts by it.
	/// </summary>
	public async Task<PageResult<PageUpdate>> UpdateAsync(Guid id, UpdatePageRequest req, Guid staffId, CancellationToken ct)
	{
		var page = await db.Pages.FirstOrDefaultAsync(p => p.Id == id, ct);
		if (page is null)
			return PageResult<PageUpdate>.Fail(PageError.NotFound);
		// response tag names + diff base for a tag write
		var pageTags = await db.PageTags.Include(pt => pt.Tag).Where(pt => pt.PageId == id).ToListAsync(ct);
		var config = cms.Page(page.Slug);

		var title = req.Title?.Trim();
		if (title is { Length: 0 })
			return PageResult<PageUpdate>.Fail(PageError.Invalid, "Title cannot be empty.");

		var slug = req.Slug?.Trim().ToLowerInvariant();
		if (slug is not null)
		{
			if (SlugError(slug) is { } slugError)
				return PageResult<PageUpdate>.Fail(PageError.Invalid, slugError);
			if (slug != page.Slug && config is { IsLocked: true })
				return PageResult<PageUpdate>.Fail(PageError.Invalid, "This page's slug is fixed by the site configuration.");
			if (slug != page.Slug && await db.Pages.AnyAsync(p => p.Slug == slug, ct))
				return PageResult<PageUpdate>.Fail(PageError.SlugTaken, $"Slug '{slug}' is already in use.");
		}

		if ((MetaError(req.MetaTitle, MaxMetaTitleLength, "Meta title")
			?? MetaError(req.MetaDescription, MaxMetaDescriptionLength, "Meta description")) is { } metaError)
			return PageResult<PageUpdate>.Fail(PageError.Invalid, metaError);

		if (req.Blocks is not null)
		{
			if (config is { Editable: false })
				return PageResult<PageUpdate>.Fail(PageError.Invalid, "This page's content comes from its template.");
			if (ValidateBlocks(req.Blocks) is { } blockError)
				return PageResult<PageUpdate>.Fail(PageError.Invalid, blockError);
		}

		Dictionary<string, PageTextTranslation>? translations = null;
		if (req.Translations is not null)
		{
			(translations, var translationError) = NormalizeTranslations(req.Translations);
			if (translationError is not null)
				return PageResult<PageUpdate>.Fail(PageError.Invalid, translationError);
		}

		List<string>? tagNames = null;
		if (req.Tags is not null)
		{
			(tagNames, var tagError) = NormalizeTags(req.Tags);
			if (tagError is not null)
				return PageResult<PageUpdate>.Fail(PageError.Invalid, tagError);
		}

		var (wasPublished, oldLiveTitle, oldSlug) = (page.Status == PageStatus.Published, page.PublishedTitle, page.Slug);
		var oldLiveTitles = LiveTitles(page.PublishedTranslations);
		if (title is not null) page.Title = title;
		if (slug is not null) page.Slug = slug;
		if (req.MetaTitle is not null) page.MetaTitle = req.MetaTitle.Trim();
		if (req.MetaDescription is not null) page.MetaDescription = req.MetaDescription.Trim();
		if (translations is not null) page.Translations = translations;
		if (req.Blocks is not null)
		{
			page.Blocks = req.Blocks;
			page.BlockCount = CountBlocks(req.Blocks);
		}

		var publish = req.Status == PageStatus.Published;
		var unpublish = req.Status == PageStatus.Draft && wasPublished;
		if (publish)
		{
			page.PublishedTitle = page.Title;
			page.PublishedMetaTitle = page.MetaTitle;
			page.PublishedMetaDescription = page.MetaDescription;
			page.PublishedBlocks = CloneBlocks(page.Blocks);
			page.PublishedTranslations = new(page.Translations);
			page.Status = PageStatus.Published;
			page.PublishedAt = DateTimeOffset.UtcNow;
		}
		else if (unpublish)
		{
			page.Status = PageStatus.Draft;
			page.PublishedAt = null;
		}

		var contentSent = req.Title is not null || req.Blocks is not null || req.MetaTitle is not null
			|| req.MetaDescription is not null || req.Translations is not null;
		if (contentSent || publish || unpublish)
			page.HasUnpublishedChanges = page.Status == PageStatus.Published && !MatchesSnapshot(page);
		if (contentSent || req.Slug is not null || publish || unpublish)
		{
			page.UpdatedAt = DateTimeOffset.UtcNow;
			page.UpdatedById = staffId;
		}

		var tagRemoved = false;
		if (tagNames is not null)
		{
			var removed = pageTags.Where(pt => !tagNames.Contains(pt.Tag.Name)).ToList();
			db.PageTags.RemoveRange(removed);
			tagRemoved = removed.Count > 0;
			var added = tagNames.Where(n => pageTags.All(pt => pt.Tag.Name != n)).ToList();
			if (added.Count > 0)
				foreach (var tag in await LoadOrCreateTagsAsync(added, ct))
					db.PageTags.Add(new PageTag { PageId = page.Id, Tag = tag });
		}

		await db.SaveChangesAsync(ct);
		if (tagRemoved)
			await DeleteUnusedTagsAsync(ct);

		var tags = (tagNames ?? pageTags.Select(pt => pt.Tag.Name).ToList()).Order(StringComparer.Ordinal).ToList();
		var live = page.Status == PageStatus.Published;
		var statusChanged = live != wasPublished;
		// a translated title shows in other pages' menus/footers too
		var liveRenamed = live && (page.PublishedTitle != oldLiveTitle || page.Slug != oldSlug
			|| LiveTitles(page.PublishedTranslations) != oldLiveTitles);
		return PageResult<PageUpdate>.Ok(new PageUpdate(ToSummary(page, tags), statusChanged,
			live && (publish || page.Slug != oldSlug), liveRenamed));
	}

	private static readonly JsonSerializerOptions BlockJson = new(JsonSerializerDefaults.Web);

	private static List<Block> CloneBlocks(List<Block> blocks) =>
		JsonSerializer.Deserialize<List<Block>>(JsonSerializer.Serialize(blocks, BlockJson), BlockJson)!;

	/// <summary>
	/// Working copy = live snapshot (block trees compared as JSON: attribute order counts; page translations by
	/// language, order-independent).
	/// </summary>
	private static bool MatchesSnapshot(Page p) =>
		p.PublishedBlocks is not null && p.Title == p.PublishedTitle && p.MetaTitle == p.PublishedMetaTitle
		&& p.MetaDescription == p.PublishedMetaDescription
		&& p.Translations.Count == p.PublishedTranslations.Count
		&& p.Translations.All(t => p.PublishedTranslations.GetValueOrDefault(t.Key) == t.Value)
		&& JsonSerializer.Serialize(p.Blocks, BlockJson) == JsonSerializer.Serialize(p.PublishedBlocks, BlockJson);

	// "de=Titel|fr=Titre": compared as a whole (any language's live title changed)
	private static string LiveTitles(Dictionary<string, PageTextTranslation> translations) =>
		string.Join('|', translations.Where(t => t.Value.Title.Length > 0).OrderBy(t => t.Key, StringComparer.Ordinal)
			.Select(t => $"{t.Key}={t.Value.Title}"));

	/// <summary>
	/// Trimmed title/meta translations, entries without any text dropped, or the first error (code not a language
	/// code, text over the default fields' limits). Codes need not be configured: removing a language from the config
	/// and adding it back keeps its text.
	/// </summary>
	private static (Dictionary<string, PageTextTranslation> Value, string? Error) NormalizeTranslations(
		Dictionary<string, PageTextTranslation> raw)
	{
		var result = new Dictionary<string, PageTextTranslation>();
		foreach (var (code, value) in raw)
		{
			if (!LangCodePattern().IsMatch(code))
				return (result, $"'{code}' is not a language code.");
			var text = new PageTextTranslation(value?.Title?.Trim() ?? "", value?.MetaTitle?.Trim() ?? "",
				value?.MetaDescription?.Trim() ?? "");
			if (value?.Sources is { } hashes)
			{
				// a source only next to a translated field
				var sources = new PageTextSources(text.Title.Length > 0 ? hashes.Title ?? "" : "",
					text.MetaTitle.Length > 0 ? hashes.MetaTitle ?? "" : "", text.MetaDescription.Length > 0 ? hashes.MetaDescription ?? "" : "");
				if (new[] { sources.Title, sources.MetaTitle, sources.MetaDescription }.Any(h => h.Length > MaxSourceHashLength))
					return (result, $"Translation sources ({code}) must be at most {MaxSourceHashLength} characters.");
				if (sources != new PageTextSources())
					text = text with { Sources = sources };
			}
			if (text.Title.Length > MaxTitleLength)
				return (result, $"Title ({code}) must be at most {MaxTitleLength} characters.");
			if ((MetaError(text.MetaTitle, MaxMetaTitleLength, $"Meta title ({code})")
				?? MetaError(text.MetaDescription, MaxMetaDescriptionLength, $"Meta description ({code})")) is { } error)
				return (result, error);
			if (text with { Sources = null } != new PageTextTranslation())
				result[code] = text;
		}
		return (result, null);
	}

	// null = not sent (unchanged); empty clears
	private static string? MetaError(string? value, int max, string label) =>
		value?.Trim().Length > max ? $"{label} must be at most {max} characters." : null;

	public async Task<PageResult<bool>> DeleteAsync(Guid id, CancellationToken ct)
	{
		var page = await db.Pages.AsNoTracking()
			.Where(p => p.Id == id)
			.Select(p => new { p.Slug, HasTags = p.PageTags.Any() })
			.FirstOrDefaultAsync(ct);
		if (page is null)
			return PageResult<bool>.Fail(PageError.NotFound);
		if (cms.Page(page.Slug) is { IsLocked: true })
			return PageResult<bool>.Fail(PageError.Invalid, "This page is part of the site configuration and can't be deleted.");
		// page_tags + rendered_pages cascade in Postgres
		if (await db.Pages.Where(p => p.Id == id).ExecuteDeleteAsync(ct) == 0)
			return PageResult<bool>.Fail(PageError.NotFound);
		if (page.HasTags)
			await DeleteUnusedTagsAsync(ct);
		return PageResult<bool>.Ok(true);
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
	private Task DeleteUnusedTagsAsync(CancellationToken ct) =>
		db.Tags.Where(t => !t.PageTags.Any()).ExecuteDeleteAsync(ct);

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
	/// <see cref="ValidateSlug"/> + the site's language codes on a localized site (URL prefixes: <c>/de</c> is the
	/// German home). <see cref="CmsConfigLoader"/> keeps codes and configured page slugs apart.
	/// </summary>
	private string? SlugError(string slug) =>
		ValidateSlug(slug) ?? (IsLanguageCode(slug) ? $"Slug '{slug}' is reserved." : null);

	private bool IsLanguageCode(string slug) => cms.Site.Lang.IsLocalized && cms.Site.Lang.Codes.Contains(slug);

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
					if (!IsPrimitive(value))
						return $"Attribute '{key}' of block '{block.Id}' must be a string, number or boolean.";
				}
				if (block.Translations is not null)
				{
					foreach (var (code, values) in block.Translations)
					{
						if (!LangCodePattern().IsMatch(code))
							return $"Block '{block.Id}': '{code}' is not a language code.";
						if (values is null)
							return $"Block '{block.Id}': translations of '{code}' must be an object.";
						foreach (var (key, value) in values)
							if (!IsPrimitive(value))
								return $"Translation '{code}.{key}' of block '{block.Id}' must be a string, number or boolean.";
					}
					// stored without empty languages; none left = no translations key
					foreach (var code in block.Translations.Where(t => t.Value.Count == 0).Select(t => t.Key).ToList())
						block.Translations.Remove(code);
					if (block.Translations.Count == 0)
						block.Translations = null;
				}
				if (block.TranslationSources is not null)
				{
					foreach (var (code, hashes) in block.TranslationSources)
					{
						if (!LangCodePattern().IsMatch(code))
							return $"Block '{block.Id}': '{code}' is not a language code.";
						if (hashes is null || hashes.Values.Any(h => h is null || h.Length > MaxSourceHashLength))
							return $"Block '{block.Id}': translation sources of '{code}' must be strings of at most {MaxSourceHashLength} characters.";
					}
					foreach (var code in block.TranslationSources.Where(t => t.Value.Count == 0).Select(t => t.Key).ToList())
						block.TranslationSources.Remove(code);
					if (block.TranslationSources.Count == 0)
						block.TranslationSources = null;
				}
				if (Walk(block.InnerBlocks, depth + 1) is { } error)
					return error;
			}
			return null;
		}
	}

	private static bool IsPrimitive(JsonElement value) =>
		value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False;

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
		if (ReservedSlugs.Contains(baseSlug) || IsLanguageCode(baseSlug))
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

	/// <summary>Blocks at all depths (<see cref="Page.BlockCount"/>).</summary>
	public static int CountBlocks(List<Block> blocks) =>
		blocks.Sum(b => 1 + CountBlocks(b.InnerBlocks ?? []));

	private PageSummary ToSummary(Page p, List<string> tags) =>
		new(p.Id, p.Title, p.Slug, p.Status, tags, p.BlockCount, p.CreatedAt, p.UpdatedAt, p.PublishedAt,
			p.HasUnpublishedChanges, cms.Page(p.Slug) is { IsLocked: true });

	/// <param name="tags">sorted names (every caller knows them: no tags query)</param>
	private async Task<PageDetail> ToDetailAsync(Page p, List<string> tags, CancellationToken ct)
	{
		var config = cms.Page(p.Slug);
		return new(p.Id, p.Title, p.Slug, p.Status, tags,
			p.Blocks, await MediaService.ResolveRefsAsync(db, p.Blocks, ct), p.CreatedAt, p.UpdatedAt, p.PublishedAt,
			p.HasUnpublishedChanges, config?.Template, config?.IsLocked ?? false, config?.Editable ?? true, p.MetaTitle, p.MetaDescription,
			p.Translations);
	}
}
