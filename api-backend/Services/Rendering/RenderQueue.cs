using System.Threading.Channels;

namespace api_backend.Services.Rendering;

/// <summary>Asks the <see cref="RenderWorker"/> to re-render public pages after content changes.</summary>
public interface IRenderQueue
{
	/// <summary>One published page's content changed.</summary>
	void Page(Guid pageId);

	/// <summary>Something every page shows changed (menu, site config, media, publish state, a title/slug).</summary>
	void All();
}

/// <summary>Unbounded in-memory queue; <c>null</c> = all pages. The worker coalesces bursts (autosave).</summary>
public sealed class RenderQueue(RenderStatus status) : IRenderQueue
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
		_channel.Writer.TryWrite(null);
	}

	/// <summary>Re-render without marking stored HTML stale (no data changed: deploy, startup, retry). <c>null</c> = all.</summary>
	public void Requeue(Guid? pageId) => _channel.Writer.TryWrite(pageId);
}
