using System.Text.RegularExpressions;
using api_backend.Models.Dto;
using api_backend.Models.Menus;
using api_backend.Models.Pages;
using api_backend.Services.Cms;
using api_backend.Services.Pages;
using api_backend.Services.Site;
using Microsoft.EntityFrameworkCore;

namespace api_backend.Services.Menus;

/// <summary>
/// Menus: CRUD for the admin view, validation of the item tree, and the public (resolved) form.
/// Errors reuse <see cref="PageResult{T}"/>: <see cref="PageError.SlugTaken"/> = handle taken.
/// </summary>
public partial class MenuService(AppDbContext db)
{
	[GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
	private static partial Regex HandlePattern();

	/// <summary>
	/// Creates the main menu when missing (fresh install, or deleted directly in the DB), with a Home link (+ Login when
	/// the instance config enables public auth) the owner can edit or remove.
	/// </summary>
	public static async Task EnsureMainAsync(IServiceProvider services, CancellationToken ct = default)
	{
		using var scope = services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		if (await db.Menus.AnyAsync(m => m.Handle == Menu.MainHandle, ct)) return;
		List<MenuItem> items = [new MenuItem { Id = "home", Label = "Home", Url = "/" }];
		// customer login only exists with public auth (instance config)
		if (scope.ServiceProvider.GetRequiredService<CmsConfig>().PublicAuth)
			items.Add(new MenuItem { Id = "login", Label = "Login", Url = "/login" });
		db.Menus.Add(new Menu { Handle = Menu.MainHandle, Items = items });
		await db.SaveChangesAsync(ct);
	}

	public async Task<List<MenuSummary>> ListAsync(CancellationToken ct)
	{
		var menus = await db.Menus.AsNoTracking().ToListAsync(ct);
		// main first, then alphabetical
		return menus
			.OrderBy(m => m.Handle != Menu.MainHandle).ThenBy(m => m.Handle)
			.Select(m => new MenuSummary(m.Id, m.Handle, Count(m.Items), m.UpdatedAt))
			.ToList();
	}

	public async Task<MenuDetail?> GetAsync(Guid id, CancellationToken ct)
	{
		var menu = await db.Menus.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
		return menu is null ? null : ToDetail(menu);
	}

	public async Task<PageResult<MenuDetail>> CreateAsync(CreateMenuRequest req, Guid staffId, CancellationToken ct)
	{
		var handle = NormalizeHandle(req.Handle);
		if (ValidateHandle(handle) is { } error)
			return PageResult<MenuDetail>.Fail(PageError.Invalid, error);
		if (await db.Menus.AnyAsync(m => m.Handle == handle, ct))
			return PageResult<MenuDetail>.Fail(PageError.SlugTaken, $"Handle '{handle}' is already in use.");

		var menu = new Menu { Handle = handle, UpdatedById = staffId };
		db.Menus.Add(menu);
		await db.SaveChangesAsync(ct);
		return PageResult<MenuDetail>.Ok(ToDetail(menu));
	}

	public async Task<PageResult<MenuDetail>> UpdateAsync(Guid id, UpdateMenuRequest req, Guid staffId, CancellationToken ct)
	{
		var menu = await db.Menus.FirstOrDefaultAsync(m => m.Id == id, ct);
		if (menu is null)
			return PageResult<MenuDetail>.Fail(PageError.NotFound);

		var items = req.Items ?? [];
		if (await ValidateItemsAsync(items, ct) is { } itemError)
			return PageResult<MenuDetail>.Fail(PageError.Invalid, itemError);

		menu.Items = items;
		menu.UpdatedAt = DateTimeOffset.UtcNow;
		menu.UpdatedById = staffId;
		await db.SaveChangesAsync(ct);
		return PageResult<MenuDetail>.Ok(ToDetail(menu));
	}

	public async Task<PageResult<bool>> DeleteAsync(Guid id, CancellationToken ct)
	{
		var menu = await db.Menus.FirstOrDefaultAsync(m => m.Id == id, ct);
		if (menu is null)
			return PageResult<bool>.Fail(PageError.NotFound);
		if (menu.Handle == Menu.MainHandle)
			return PageResult<bool>.Fail(PageError.Invalid, "The main menu can't be deleted.");
		db.Menus.Remove(menu);
		await db.SaveChangesAsync(ct);
		return PageResult<bool>.Ok(true);
	}

	/// <summary>
	/// The menu for the public site. Page links get the page's current slug (and title when unlabeled);
	/// a link to an unpublished/deleted page becomes a folder when labeled, otherwise its children move up
	/// a level. Folders left without children are dropped.
	/// </summary>
	public async Task<PublicMenu?> GetPublicAsync(string handle, CancellationToken ct)
	{
		var normalized = handle.ToLowerInvariant();
		var menu = await db.Menus.AsNoTracking().FirstOrDefaultAsync(m => m.Handle == normalized, ct);
		if (menu is null)
			return null;

		var ids = PageIds(menu.Items);
		var pages = await db.Pages.AsNoTracking()
			.Where(p => ids.Contains(p.Id) && p.Status == PageStatus.Published)
			.Select(p => new { p.Id, p.Title, p.Slug })
			.ToDictionaryAsync(p => p.Id, ct);

		return new PublicMenu(menu.Handle, Resolve(menu.Items));

		List<PublicMenuItem> Resolve(List<MenuItem> items)
		{
			var result = new List<PublicMenuItem>();
			foreach (var item in items)
			{
				var children = Resolve(item.Children);
				if (item.Url is not null)
					result.Add(new PublicMenuItem(item.Label, null, item.Url, children));
				else if (item.PageId is { } pageId && pages.TryGetValue(pageId, out var page))
					result.Add(new PublicMenuItem(item.Label.Length > 0 ? item.Label : page.Title, page.Slug, null, children));
				else if (item.Label.Length == 0)
					result.AddRange(children);
				else if (children.Count > 0)
					result.Add(new PublicMenuItem(item.Label, null, null, children));
			}
			return result;
		}
	}

	/// <summary>"Footer Links" → "footer-links"; empty when nothing usable is left. Mirrored by the frontend's <c>normalizeHandle</c>.</summary>
	public static string NormalizeHandle(string raw) =>
		raw.Any(char.IsLetterOrDigit) ? PageService.Slugify(raw) : "";

	public static string? ValidateHandle(string handle)
	{
		if (handle.Length is 0 or > MenuLimits.MaxHandleLength)
			return $"Handle must be 1-{MenuLimits.MaxHandleLength} characters.";
		if (!HandlePattern().IsMatch(handle))
			return "Handle may only contain lowercase letters, digits and single hyphens.";
		return null;
	}

	/// <summary>
	/// Returns an error for the first invalid item, or null. Normalizes in place: trims labels/URLs,
	/// empty URL → null, null children → empty.
	/// </summary>
	private async Task<string?> ValidateItemsAsync(List<MenuItem> items, CancellationToken ct)
	{
		var count = 0;
		var ids = new HashSet<string>();
		if (Walk(items, 1) is { } error)
			return error;

		var pageIds = PageIds(items);
		var existing = await db.Pages.Where(p => pageIds.Contains(p.Id)).CountAsync(ct);
		return existing == pageIds.Count ? null : "A menu item links to a page that doesn't exist.";

		string? Walk(List<MenuItem> level, int depth)
		{
			if (depth > MenuLimits.MaxDepth)
				return $"Menu items may be nested at most {MenuLimits.MaxDepth} levels deep.";
			foreach (var item in level)
			{
				if (item is null)
					return "Menu items must not be null.";
				if (++count > MenuLimits.MaxItems)
					return $"A menu may contain at most {MenuLimits.MaxItems} items.";
				if (string.IsNullOrWhiteSpace(item.Id) || item.Id.Length > 64 || !ids.Add(item.Id))
					return "Every menu item needs a unique id of at most 64 characters.";

				item.Label = item.Label?.Trim() ?? "";
				item.Url = string.IsNullOrWhiteSpace(item.Url) ? null : item.Url.Trim();
				item.Children ??= [];

				if (item.Label.Length > MenuLimits.MaxLabelLength)
					return $"Labels may be at most {MenuLimits.MaxLabelLength} characters.";
				if (item.Url is not null && item.PageId is not null)
					return $"\"{item.Label}\" links to a page and a URL; pick one.";
				if (item.Url is not null && (item.Url.Length > MenuLimits.MaxUrlLength || !IsMenuUrl(item.Url)))
					return $"\"{item.Label}\" needs a path like /about or an http(s) URL.";
				if (item.Label.Length == 0 && item.PageId is null)
					return "Items without a page link need a label.";
				if (Walk(item.Children, depth + 1) is { } error)
					return error;
			}
			return null;
		}
	}

	/// <summary>
	/// Absolute http(s) URL or a same-site path ("/about", "/admin/pages"); rendered as href. "//host" is
	/// protocol-relative (another site), so rejected, as is whitespace.
	/// </summary>
	public static bool IsMenuUrl(string url) =>
		SiteConfigService.IsHttpUrl(url)
		|| (url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\") && !url.Any(char.IsWhiteSpace));

	private static HashSet<Guid> PageIds(List<MenuItem> items)
	{
		var ids = new HashSet<Guid>();
		Collect(items);
		return ids;

		void Collect(List<MenuItem> level)
		{
			foreach (var item in level)
			{
				if (item.PageId is { } id)
					ids.Add(id);
				Collect(item.Children);
			}
		}
	}

	private static int Count(List<MenuItem> items) => items.Sum(i => 1 + Count(i.Children));

	private static MenuDetail ToDetail(Menu m) => new(m.Id, m.Handle, m.Items, m.CreatedAt, m.UpdatedAt);
}
