using System.Collections.Concurrent;

namespace api_backend.Services.Rendering;

/// <summary>
/// Render state shared by the queue/worker (write) and <c>PublicHtmlController</c> (read). In memory: assumes one
/// API instance (as does <see cref="RenderQueue"/>).
/// </summary>
public sealed class RenderStatus
{
	/// <summary>Version the renderer reports now; stored HTML of another version references missing assets.</summary>
	public volatile string? CurrentVersion;

	/// <summary>
	/// Not-found page per language code (rendered with the current menu/footer); kept in memory, re-rendered with
	/// everything. Replaced as a whole.
	/// </summary>
	public volatile IReadOnlyDictionary<string, string>? NotFoundHtml;

	/// <summary>
	/// Customer screens by language and <see cref="PublicScreens"/> path (<see cref="RenderedScreens.Key"/>), like
	/// <see cref="NotFoundHtml"/> (empty with public auth off). Replaced as a whole.
	/// </summary>
	public volatile RenderedScreens? Screens;

	private long _allChangedAt;
	private long _refreshedAt;
	private readonly ConcurrentDictionary<Guid, DateTimeOffset> _pageChangedAt = new();

	/// <summary>
	/// Restore only: <c>rendered_pages</c> is excluded from backups (empty after a restore), but a render in flight
	/// during the restore could store pre-restore HTML afterwards; this makes that row stale.
	/// </summary>
	public void MarkAllChanged() => Interlocked.Exchange(ref _allChangedAt, DateTimeOffset.UtcNow.UtcTicks);

	/// <summary>
	/// Quiet change (menu, site config, media, publish state, a title/slug), set at save time: stored HTML stays servable, but HTML rendered before
	/// it gets the refresh marker (<see cref="NeedsRefresh"/>). Separate from <see cref="MarkAllChanged"/> on purpose:
	/// that one means "serve the shell instead", this one only "serve it, the client refetches menu/config". An API
	/// restart during the re-render drops the marker for pages not re-rendered yet (old menu for seconds, harmless).
	/// </summary>
	public void MarkRefreshed() => Interlocked.Exchange(ref _refreshedAt, DateTimeOffset.UtcNow.UtcTicks);

	public void MarkChanged(Guid pageId) => _pageChangedAt[pageId] = DateTimeOffset.UtcNow;

	/// <summary>
	/// Stored HTML rendered before the last change affecting the page is stale: the controller serves the
	/// client-rendered shell (live data) instead, so an edit shows right away, not after the re-render.
	/// </summary>
	public bool IsFresh(Guid pageId, DateTimeOffset renderedAt)
	{
		var all = new DateTimeOffset(Interlocked.Read(ref _allChangedAt), TimeSpan.Zero);
		return renderedAt > all && (!_pageChangedAt.TryGetValue(pageId, out var page) || renderedAt > page);
	}

	/// <summary>Stored HTML rendered before the last quiet change: served, but the client refetches menu/config.</summary>
	public bool NeedsRefresh(DateTimeOffset renderedAt) =>
		renderedAt.UtcTicks <= Interlocked.Read(ref _refreshedAt);
}

/// <summary>
/// Screen HTML of one full render by <see cref="Key"/>; <see cref="RenderedAt"/> = its start (refresh marker check),
/// <see cref="Version"/> = the renderer build (served only while current).
/// </summary>
public sealed record RenderedScreens(IReadOnlyDictionary<string, string> Html, DateTimeOffset RenderedAt, string Version)
{
	/// <summary><c>de:user/profile</c></summary>
	public static string Key(string code, string path) => $"{code}:{path}";
}
