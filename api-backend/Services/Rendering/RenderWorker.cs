using System.Diagnostics;
using api_backend.Models.Dto;
using api_backend.Models.Menus;
using api_backend.Models.Pages;
using api_backend.Models.Rendering;
using api_backend.Services.Cms;
using api_backend.Services.Menus;
using api_backend.Services.Pages;
using api_backend.Services.Site;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace api_backend.Services.Rendering;

/// <summary>
/// Pre-renders published pages to HTML (stored in <c>rendered_pages</c>, served by <c>PublicHtmlController</c>)
/// so nothing renders at visitor request time. Driven by <see cref="RenderQueue"/> (content changes) and a
/// periodic check that re-renders everything when the renderer's version changed (frontend deploy) or a
/// published page has no current HTML (renderer was down, fresh install). Full renders also produce the in-memory
/// not-found page and customer screens. Everything renders once per configured language.
/// </summary>
public sealed class RenderWorker(
	RenderQueue queue,
	CmsConfig cms,
	IRendererClient renderer,
	RenderStatus status,
	IServiceScopeFactory scopeFactory,
	IOptions<RendererOptions> options,
	ILogger<RenderWorker> logger) : BackgroundService
{
	private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);
	private const int ChunkSize = 20;

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!renderer.Enabled)
		{
			logger.LogInformation("Renderer:BaseUrl not set; public pages are served as the client-rendered shell.");
			return;
		}
		await Task.WhenAll(CheckLoopAsync(stoppingToken), RenderLoopAsync(stoppingToken));
	}

	private async Task RenderLoopAsync(CancellationToken ct)
	{
		var debounce = TimeSpan.FromMilliseconds(options.Value.DebounceMilliseconds);
		while (await queue.Reader.WaitToReadAsync(ct))
		{
			await Task.Delay(debounce, ct);
			var all = false;
			var ids = new HashSet<Guid>();
			while (queue.Reader.TryRead(out var item))
			{
				if (item is { } id) ids.Add(id);
				else all = true;
			}

			try
			{
				await RenderAsync(all, ids, ct);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				logger.LogError(ex, "Rendering public pages failed; retrying in {Delay}.", RetryDelay);
				_ = RetryLaterAsync(all, ids, ct);
			}
		}
	}

	private async Task RetryLaterAsync(bool all, HashSet<Guid> ids, CancellationToken ct)
	{
		try
		{
			await Task.Delay(RetryDelay, ct);
		}
		catch (OperationCanceledException)
		{
			return;
		}
		// already marked stale when first queued
		if (all) queue.Requeue(null);
		foreach (var id in ids) queue.Requeue(id);
	}

	private async Task CheckLoopAsync(CancellationToken ct)
	{
		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.CheckIntervalSeconds));
		var failing = false;
		do
		{
			try
			{
				await CheckAsync(ct);
				if (failing) logger.LogInformation("Renderer reachable again.");
				failing = false;
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				// renderer not up yet (compose start order), down, or not run at all (`pnpm dev`): retry each tick, warn once
				if (!failing) logger.LogWarning("Renderer version check failed, retrying: {Message}", ex.Message);
				failing = true;
			}
		} while (await timer.WaitForNextTickAsync(ct));
	}

	/// <summary>Queues a full render when the renderer version changed or a published page lacks current HTML.</summary>
	private async Task CheckAsync(CancellationToken ct)
	{
		var version = await renderer.GetVersionAsync(ct);
		var changed = status.CurrentVersion != version;
		status.CurrentVersion = version;

		using var scope = scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		var codes = cms.Site.Lang.Codes;
		// one current row per published page and language (key: page + lang), so counts tell; a language added to the
		// config counts as missing HTML
		var published = await db.Pages.CountAsync(p => p.Status == PageStatus.Published, ct);
		var rendered = await db.RenderedPages.CountAsync(r => r.RendererVersion == version && codes.Contains(r.Lang)
			&& r.Page!.Status == PageStatus.Published, ct);
		var stale = rendered < published * codes.Count;

		if (changed || stale || status.NotFoundHtml is null || status.Screens is null)
		{
			if (changed) logger.LogInformation("Renderer version {Version}; re-rendering public pages.", version);
			queue.Requeue(null);
		}
	}

	private async Task RenderAsync(bool all, HashSet<Guid> ids, CancellationToken ct)
	{
		// before reading any data: a change committed during this render stays newer than its HTML. Truncated to
		// microseconds (Postgres precision), so the stored RenderedAt equals it.
		var now = DateTimeOffset.UtcNow;
		var startedAt = now.AddTicks(-(now.UtcTicks % 10));
		using var scope = scopeFactory.CreateScope();
		var services = scope.ServiceProvider;
		var db = services.GetRequiredService<AppDbContext>();
		var codes = cms.Site.Lang.Codes;
		var defaultCode = cms.Site.Lang.DefaultCode;

		// a single page widens to all when it left the public site, is new there, or changed its live title/slug (menus +
		// footer links on every page show those). Safety net: the controllers queue Refresh() for those already (it
		// marks other pages for the client refresh at save time).
		if (!all)
		{
			var pages = await db.Pages.AsNoTracking()
				.Where(p => ids.Contains(p.Id))
				// default language only: the title it was rendered with is the default one
				.LeftJoin(db.RenderedPages.Where(r => r.Lang == defaultCode), p => p.Id, r => r.PageId, (p, r) => new
				{
					p.Status,
					Title = p.PublishedTitle,
					p.Slug,
					RenderedTitle = r == null ? null : r.Title,
					RenderedSlug = r == null ? null : r.Slug,
				})
				.ToListAsync(ct);
			// no row → null title/slug → mismatch
			all = pages.Count < ids.Count
				|| pages.Any(p => p.Status != PageStatus.Published || p.RenderedTitle != p.Title || p.RenderedSlug != p.Slug);
		}

		// main menu + site config per language (page titles in them are translated; the first miss loads all languages)
		var cache = services.GetRequiredService<PublicCache>();
		var (menuService, siteConfig) = (services.GetRequiredService<MenuService>(), services.GetRequiredService<SiteConfigService>());
		var menus = new Dictionary<string, PublicMenu?>();
		var configs = new Dictionary<string, SiteConfigResponse>();
		foreach (var code in codes)
		{
			menus[code] = await cache.MenuAsync(menuService, Menu.MainHandle, code, ct);
			configs[code] = await cache.SiteConfigAsync(siteConfig, code, ct);
		}
		var pageService = services.GetRequiredService<PageService>();
		var baseUrl = options.Value.PublicBaseUrl.TrimEnd('/');

		var stopwatch = Stopwatch.StartNew();
		// one page: GetPublishedByIdsAsync drops it when unpublished meanwhile
		var targets = all
			? await db.Pages.AsNoTracking().Where(p => p.Status == PageStatus.Published).Select(p => p.Id).ToListAsync(ct)
			: ids.ToList();

		// per chunk: pages + media refs in two queries, existing row keys in one, one commit (one POST per page and
		// language: Node renders one at a time anyway). Existing rows are updated without reading them (Apply sets
		// every column), so the old HTML never leaves the DB. A page is fresh once its chunk commits.
		var count = 0;
		foreach (var chunk in targets.Chunk(ChunkSize))
		{
			var published = await pageService.GetPublishedByIdsAsync(chunk, codes, ct);
			var existing = (await db.RenderedPages.Where(r => chunk.Contains(r.PageId))
				.Select(r => new { r.PageId, r.Lang }).ToListAsync(ct))
				.Select(r => (r.PageId, r.Lang)).ToHashSet();
			var results = new List<(Guid Id, string Lang, PublicPage Page, RenderResult Result)>();
			foreach (var id in chunk)
			{
				// unpublished meanwhile: its queued job widens to all and cleans up
				if (!published.TryGetValue(id, out var languages)) continue;
				foreach (var code in codes)
				{
					var page = languages[code];
					var result = await renderer.RenderAsync(
						new RenderState(page.Slug, page, menus[code], configs[code], baseUrl, code), ct);
					results.Add((id, code, page, result));
					Track(db, id, code, existing.Contains((id, code)), page, result, startedAt);
				}
			}
			try
			{
				await db.SaveChangesAsync(ct);
			}
			catch (DbUpdateException)
			{
				// a page deleted mid-render (FK): store the chunk one by one, skipping that one
				db.ChangeTracker.Clear();
				foreach (var (id, code, page, result) in results)
					await StoreAsync(db, id, code, page, result, startedAt, ct);
			}
			db.ChangeTracker.Clear(); // don't keep every page's HTML tracked
			count += results.Count / codes.Count;
		}

		if (all)
		{
			await db.RenderedPages.Where(r => r.Page!.Status != PageStatus.Published || !codes.Contains(r.Lang))
				.ExecuteDeleteAsync(ct);
			var notFound = new Dictionary<string, string>();
			var screens = new Dictionary<string, string>();
			string? version = null;
			foreach (var code in codes)
			{
				var result = await renderer.RenderAsync(
					new RenderState("not-found", null, menus[code], configs[code], baseUrl, code), ct);
				notFound[code] = result.Html;
				version ??= result.Version; // default language's
				// never served with public auth off (PublicHtmlController): don't render them either
				if (cms.PublicAuth)
					foreach (var path in PublicScreens.All)
						screens[RenderedScreens.Key(code, path)] =
							(await renderer.RenderAsync(new RenderState(path, null, menus[code], configs[code], baseUrl, code), ct)).Html;
			}
			status.NotFoundHtml = notFound;
			status.Screens = new RenderedScreens(screens, startedAt, version!);
			status.CurrentVersion = version;
		}
		logger.LogInformation("Rendered {Count} public page(s){All} in {Languages} language(s) in {Milliseconds} ms.",
			count, all ? " (all)" : "", codes.Count, stopwatch.ElapsedMilliseconds);
	}

	private static async Task StoreAsync(
		AppDbContext db, Guid pageId, string code, PublicPage page, RenderResult result, DateTimeOffset renderedAt,
		CancellationToken ct)
	{
		var exists = await db.RenderedPages.AnyAsync(r => r.PageId == pageId && r.Lang == code, ct);
		Track(db, pageId, code, exists, page, result, renderedAt);
		try
		{
			await db.SaveChangesAsync(ct);
		}
		catch (DbUpdateException)
		{
			// page deleted mid-render (FK): its queued job cleans up
			db.ChangeTracker.Clear();
		}
	}

	/// <summary>Insert, or a full-row update of an existing row without loading it.</summary>
	private static void Track(
		AppDbContext db, Guid pageId, string code, bool exists, PublicPage page, RenderResult result,
		DateTimeOffset renderedAt)
	{
		var row = new RenderedPage { PageId = pageId, Lang = code, Html = "", Title = "", Slug = "", RendererVersion = "" };
		Apply(row, page, result, renderedAt);
		if (exists) db.RenderedPages.Update(row);
		else db.RenderedPages.Add(row);
	}

	private static void Apply(RenderedPage row, PublicPage page, RenderResult result, DateTimeOffset renderedAt)
	{
		row.Html = result.Html;
		row.Title = page.Title;
		row.Slug = page.Slug;
		row.RendererVersion = result.Version;
		row.RenderedAt = renderedAt;
	}
}
