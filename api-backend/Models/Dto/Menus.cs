using System.ComponentModel.DataAnnotations;
using api_backend.Models.Menus;

namespace api_backend.Models.Dto;

public record MenuSummary(
	Guid Id,
	string Handle,
	int ItemCount,
	DateTimeOffset UpdatedAt);

public record MenuDetail(
	Guid Id,
	string Handle,
	List<MenuItem> Items,
	DateTimeOffset CreatedAt,
	DateTimeOffset UpdatedAt);

/// <summary>Handle is normalized to kebab-case ("Footer Links" → "footer-links") and can't change later.</summary>
public record CreateMenuRequest([Required, MaxLength(MenuLimits.MaxHandleLength)] string Handle);

/// <summary>Full replace of the item tree.</summary>
public record UpdateMenuRequest(List<MenuItem> Items);

/// <summary>
/// A menu as the public site renders it: page links resolved to the page's current slug (and title when the
/// item has no label); links to unpublished or deleted pages are left out.
/// </summary>
public record PublicMenu(string Handle, List<PublicMenuItem> Items);

/// <summary>Slug = internal page link, Url = external link, neither = folder.</summary>
public record PublicMenuItem(string Label, string? Slug, string? Url, List<PublicMenuItem> Children);
