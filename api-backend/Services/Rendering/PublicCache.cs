using System.Collections.Concurrent;
using api_backend.Models.Dto;
using api_backend.Services.Menus;
using api_backend.Services.Site;

namespace api_backend.Services.Rendering;

/// <summary>
/// In-memory cache of what every public page shows around its body: resolved public menus (by handle) and the site
/// config. Hot on shell loads, <c>data-refresh</c> refetches and every render batch, but only changed by writes that
/// already call <see cref="IRenderQueue.Refresh"/>/<see cref="IRenderQueue.All"/> (which invalidate it) or by the
/// menus controller. Assumes one API instance, like <see cref="RenderStatus"/>.
/// <para>
/// Entries carry the generation they were loaded in; only the current one is served, so a load that started before
/// an invalidation (and may have read old data) is never served. Cached DTOs are shared between requests: treat
/// them as immutable (they are only serialized).
/// </para>
/// </summary>
public sealed class PublicCache
{
	private const string ConfigKey = "config";

	private long _generation;
	private readonly ConcurrentDictionary<string, (long Generation, object Value)> _entries = new();

	/// <summary>Call after the commit of any write that changes a public menu or the site config.</summary>
	public void Invalidate()
	{
		Interlocked.Increment(ref _generation);
		_entries.Clear();
	}

	/// <summary><c>null</c> (unknown handle) isn't cached, so random handles can't grow the cache.</summary>
	public Task<PublicMenu?> MenuAsync(MenuService menus, string handle, CancellationToken ct) =>
		GetAsync("menu:" + handle.ToLowerInvariant(), () => menus.GetPublicAsync(handle, ct));

	public async Task<SiteConfigResponse> SiteConfigAsync(SiteConfigService siteConfig, CancellationToken ct) =>
		(await GetAsync<SiteConfigResponse>(ConfigKey, async () => await siteConfig.GetAsync(ct)))!;

	private async Task<T?> GetAsync<T>(string key, Func<Task<T?>> load) where T : class
	{
		// read before loading: an invalidation during the load leaves this entry outdated
		var generation = Interlocked.Read(ref _generation);
		if (_entries.TryGetValue(key, out var entry) && entry.Generation == generation)
			return (T)entry.Value;
		var value = await load();
		if (value is not null)
			_entries[key] = (generation, value);
		return value;
	}
}
