using api_backend.Models.Auth;

namespace api_backend.Models.Menus;

/// <summary>
/// A navigation menu: a tree of <see cref="MenuItem"/>s in one <c>jsonb</c> column, identified by its
/// <see cref="Handle"/> (set on creation, never changed: site code loads menus by it). The public site renders
/// <see cref="MainHandle"/> as its main navigation; other handles (e.g. a footer menu) work the same way.
/// </summary>
public class Menu
{
	public const string MainHandle = "main";

	public Guid Id { get; set; } = Guid.NewGuid();
	public required string Handle { get; set; }

	public List<MenuItem> Items { get; set; } = new();

	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
	public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
	public Guid? UpdatedById { get; set; }
	public Staff? UpdatedBy { get; set; }
}

/// <summary>
/// Links to a page (<see cref="PageId"/>, resolved to its current slug when served), a <see cref="Url"/>
/// (absolute http(s) or a same-site path like "/about"), or nothing (a folder that only groups <see cref="Children"/>).
/// An empty <see cref="Label"/> on a page link means "use the page title".
/// </summary>
public class MenuItem
{
	public required string Id { get; set; }
	public string Label { get; set; } = "";
	public Guid? PageId { get; set; }
	public string? Url { get; set; }
	public List<MenuItem> Children { get; set; } = new();
}

public static class MenuLimits
{
	public const int MaxHandleLength = 50;
	public const int MaxLabelLength = 100;
	public const int MaxUrlLength = 1000;
	public const int MaxDepth = 10;
	public const int MaxItems = 200;
}
