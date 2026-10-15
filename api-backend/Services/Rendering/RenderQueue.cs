using System.Threading.Channels;

namespace api_backend.Services.Rendering;

/// <summary>Asks the <see cref="RenderWorker"/> to re-render public pages after content changes.</summary>
public interface IRenderQueue
{
	/// <summary>One published page's content changed.</summary>
	void Page(Guid pageId);

	/// <summary>
	/// Page bodies changed wholesale (backup restore): marks all stored HTML stale, so every page is served as the
	/// client-rendered shell until re-rendered. Normal editing never needs this, see <see cref="Refresh"/>.
	/// </summary>
	void All();

	/// <summary>
	/// Something every page shows around its body changed (main menu, site config, media, publish state): re-render
	/// all pages without marking stored HTML stale. The old HTML differs only in the menu/site config, which the
	/// public client refetches for pages marked by <see cref="RenderStatus.NeedsRefresh"/> (media: alt text or a
	/// broken image for a few seconds).
	/// </summary>
	void Refresh();
}

/// <summary>Unbounded in-memory queue; <c>null</c> = all pages. The worker coalesces bursts (autosave).</summary>
/// <remarks>
/// <see cref="Refresh"/> and <see cref="All"/> also drop the <see cref="PublicCache"/>: every write that changes a
/// public menu or the site config goes through one of them (non-main menus: <c>MenusController</c> invalidates).
/// </remarks>
public sealed class RenderQueue(RenderStatus status, PublicCache cache) : IRenderQueue
{
	private readonly Channel<Guid?> _channel = Channel.CreateUnbounded<Guid?>(new UnboundedChannelOptions { SingleReader = true });

	public ChannelReader<Guid?> Reader => _channel.Reader;

	public void Page(Guid pageId)
	{
		status.MarkChanged(pageId);
		_channel.Writer.TryWrite(pageId);
	}

	public void All()
	{
		status.MarkAllChanged();
		cache.Invalidate();
		_channel.Writer.TryWrite(null);
	}

	public void Refresh()
	{
		status.MarkRefreshed();
		cache.Invalidate();
		_channel.Writer.TryWrite(null);
	}

	/// <summary>Re-render without marking stored HTML stale (no data changed: deploy, startup, retry). <c>null</c> = all.</summary>
	public void Requeue(Guid? pageId) => _channel.Writer.TryWrite(pageId);
}
