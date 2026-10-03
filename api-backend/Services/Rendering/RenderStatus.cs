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

	/// <summary>Not-found page (rendered with the current menu/footer); kept in memory, re-rendered with everything.</summary>
	public volatile string? NotFoundHtml;

	private long _allChangedAt;
	private readonly ConcurrentDictionary<Guid, DateTimeOffset> _pageChangedAt = new();

	public void MarkAllChanged() => Interlocked.Exchange(ref _allChangedAt, DateTimeOffset.UtcNow.UtcTicks);

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
}
