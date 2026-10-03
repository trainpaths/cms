using api_backend.Models.Pages;

namespace api_backend.Models.Rendering;

/// <summary>
/// Server-rendered HTML of a published page (frontend <c>renderer</c> service, see <c>Services/Rendering</c>).
/// <see cref="Title"/>/<see cref="Slug"/> are the values it was rendered with: when they change, menus and
/// footers on other pages may show them too, so the worker re-renders everything.
/// </summary>
public class RenderedPage
{
	public Guid PageId { get; set; }
	public Page? Page { get; set; }

	public required string Html { get; set; }
	public required string Title { get; set; }
	public required string Slug { get; set; }

	/// <summary>Renderer build that produced <see cref="Html"/>; other builds reference other asset files.</summary>
	public required string RendererVersion { get; set; }
	public DateTimeOffset RenderedAt { get; set; } = DateTimeOffset.UtcNow;
}
