using System.Collections.Concurrent;
using api_backend.Models.Dto;
using api_backend.Services.Cms;
using api_backend.Services.Menus;
using api_backend.Services.Site;

namespace api_backend.Services.Rendering;

/// <summary>
/// In-memory cache of what every public page shows around its body: resolved public menus (by handle) and the site
/// config, per language code (page titles in them are translated; a miss loads every language at once). Hot on shell
/// loads, <c>data-refresh</c> refetches and every render batch, but only changed by writes that
/// already call <see cref="IRenderQueue.Refresh"/>/<see cref="IRenderQueue.All"/> (which invalidate it) or by the
/// menus controller. Assumes one API instance, like <see cref="RenderStatus"/>.
/// <para>
/// Entries carry the generation they were loaded in; only the current one is served, so a load that started before
/// an invalidation (and may have read old data) is never served. Cached DTOs are shared between requests: treat
/// them as immutable (they are only serialized).
/// </para>
/// </summary>
public sealed class PublicCache(CmsConfig cms)
{
	private long _generation;
	private readonly ConcurrentDictionary<string, (long Generation, object Value)> _entries = new();

	/// <summary>Call after the commit of any write that changes a public menu or the site config.</summary>
	public void Invalidate()
	{
		Interlocked.Increment(ref _generation);
		_entries.Clear();
	}

	/// <summary><c>null</c> (unknown handle) isn't cached, so random handles can't grow the cache.</summary>
	/// <param name="code">a configured language code (<c>CmsSiteLang.Resolve</c>): keys stay bounded</param>
	public Task<PublicMenu?> MenuAsync(MenuService menus, string handle, string code, CancellationToken ct)
	{
		var normalized = handle.ToLowerInvariant();
		return GetAsync(code, c => $"menu:{c}:{normalized}", codes => menus.GetPublicAsync(normalized, codes, ct));
	}

	/// <param name="code">a configured language code (<c>CmsSiteLang.Resolve</c>)</param>
	public async Task<SiteConfigResponse> SiteConfigAsync(SiteConfigService siteConfig, string code, CancellationToken ct) =>
		(await GetAsync(code, c => $"config:{c}", async codes => (Dictionary<string, SiteConfigResponse>?)
			await siteConfig.GetAsync(codes, ct)))!;

	/// <summary>The entry of <paramref name="code"/>; a miss loads and stores every configured language.</summary>
	private async Task<T?> GetAsync<T>(string code, Func<string, string> key,
		Func<IReadOnlyList<string>, Task<Dictionary<string, T>?>> load) where T : class
	{
		// read before loading: an invalidation during the load leaves these entries outdated
		var generation = Interlocked.Read(ref _generation);
		if (_entries.TryGetValue(key(code), out var entry) && entry.Generation == generation)
			return (T)entry.Value;
		var codes = cms.Site.Lang.Codes;
		var values = await load(codes.Contains(code) ? codes : [code]);
		if (values is null)
			return null;
		foreach (var (c, value) in values)
			_entries[key(c)] = (generation, value);
		return values[code];
	}
}
