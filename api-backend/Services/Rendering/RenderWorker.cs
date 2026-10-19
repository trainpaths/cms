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
/// not-found page and customer screens.
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
		var stale = await db.Pages.AnyAsync(
			p => p.Status == PageStatus.Published
				 && !db.RenderedPages.Any(r => r.PageId == p.Id && r.RendererVersion == version), ct);

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

		// a single page widens to all when it left the public site, is new there, or changed its live title/slug (menus +
		// footer links on every page show those). Safety net: the controllers queue Refresh() for those already (it
		// marks other pages for the client refresh at save time).
		if (!all)
		{
			var pages = await db.Pages.AsNoTracking()
				.Where(p => ids.Contains(p.Id))
				.LeftJoin(db.RenderedPages, p => p.Id, r => r.PageId, (p, r) => new
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

		var cache = services.GetRequiredService<PublicCache>();
		var menu = await cache.MenuAsync(services.GetRequiredService<MenuService>(), Menu.MainHandle, ct);
		var config = await cache.SiteConfigAsync(services.GetRequiredService<SiteConfigService>(), ct);
		var pageService = services.GetRequiredService<PageService>();
		var baseUrl = options.Value.PublicBaseUrl.TrimEnd('/');

		var stopwatch = Stopwatch.StartNew();
		// one page: GetPublishedByIdsAsync drops it when unpublished meanwhile
		var targets = all
			? await db.Pages.AsNoTracking().Where(p => p.Status == PageStatus.Published).Select(p => p.Id).ToListAsync(ct)
			: ids.ToList();

		// per chunk: pages + media refs in two queries, existing row ids in one, one commit (one POST per page: Node
		// renders one at a time anyway). Existing rows are updated without reading them (Apply sets every column),
		// so the old HTML never leaves the DB. A page is fresh once its chunk commits.
		var count = 0;
		foreach (var chunk in targets.Chunk(ChunkSize))
		{
			var published = await pageService.GetPublishedByIdsAsync(chunk, ct);
			var existing = await db.RenderedPages.Where(r => chunk.Contains(r.PageId)).Select(r => r.PageId).ToHashSetAsync(ct);
			var results = new List<(Guid Id, PublicPage Page, RenderResult Result)>();
			foreach (var id in chunk)
			{
				// unpublished meanwhile: its queued job widens to all and cleans up
				if (!published.TryGetValue(id, out var page)) continue;
				var result = await renderer.RenderAsync(new RenderState(page.Slug, page, menu, config, baseUrl), ct);
				results.Add((id, page, result));
				Track(db, id, existing.Contains(id), page, result, startedAt);
			}
			try
			{
				await db.SaveChangesAsync(ct);
			}
			catch (DbUpdateException)
			{
				// a page deleted mid-render (FK): store the chunk one by one, skipping that one
				db.ChangeTracker.Clear();
				foreach (var (id, page, result) in results)
					await StoreAsync(db, id, page, result, startedAt, ct);
			}
			db.ChangeTracker.Clear(); // don't keep every page's HTML tracked
			count += results.Count;
		}

		if (all)
		{
			await db.RenderedPages.Where(r => r.Page!.Status != PageStatus.Published).ExecuteDeleteAsync(ct);
			var notFound = await renderer.RenderAsync(new RenderState("not-found", null, menu, config, baseUrl), ct);
			var screens = new Dictionary<string, string>();
			// never served with public auth off (PublicHtmlController): don't render them either
			if (cms.PublicAuth)
				foreach (var path in PublicScreens.All)
					screens[path] = (await renderer.RenderAsync(new RenderState(path, null, menu, config, baseUrl), ct)).Html;
			status.NotFoundHtml = notFound.Html;
			status.Screens = new RenderedScreens(screens, startedAt, notFound.Version);
			status.CurrentVersion = notFound.Version;
		}
		logger.LogInformation("Rendered {Count} public page(s){All} in {Milliseconds} ms.",
			count, all ? " (all)" : "", stopwatch.ElapsedMilliseconds);
	}

	private static async Task StoreAsync(
		AppDbContext db, Guid pageId, PublicPage page, RenderResult result, DateTimeOffset renderedAt,
		CancellationToken ct)
	{
		var exists = await db.RenderedPages.AnyAsync(r => r.PageId == pageId, ct);
		Track(db, pageId, exists, page, result, renderedAt);
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
		AppDbContext db, Guid pageId, bool exists, PublicPage page, RenderResult result, DateTimeOffset renderedAt)
	{
		var row = new RenderedPage { PageId = pageId, Html = "", Title = "", Slug = "", RendererVersion = "" };
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
