using api_backend.Models.Menus;
using api_backend.Models.Pages;
using api_backend.Models.Rendering;
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
/// published page has no current HTML (renderer was down, fresh install).
/// </summary>
public sealed class RenderWorker(
	RenderQueue queue,
	IRendererClient renderer,
	RenderStatus status,
	IServiceScopeFactory scopeFactory,
	IOptions<RendererOptions> options,
	ILogger<RenderWorker> logger) : BackgroundService
{
	private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

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

		if (changed || stale || status.NotFoundHtml is null)
		{
			if (changed) logger.LogInformation("Renderer version {Version}; re-rendering public pages.", version);
			queue.Requeue(null);
		}
	}

	private async Task RenderAsync(bool all, HashSet<Guid> ids, CancellationToken ct)
	{
		// before reading any data: a change committed during this render stays newer than its HTML
		var startedAt = DateTimeOffset.UtcNow;
		using var scope = scopeFactory.CreateScope();
		var services = scope.ServiceProvider;
		var db = services.GetRequiredService<AppDbContext>();

		// a single page widens to all when it left the public site, is new there, or changed its title/slug
		// (menus + footer links on every page show those)
		if (!all)
		{
			foreach (var id in ids)
			{
				var page = await db.Pages.AsNoTracking()
					.Where(p => p.Id == id)
					.Select(p => new { p.Status, p.Title, p.Slug })
					.FirstOrDefaultAsync(ct);
				if (page?.Status != PageStatus.Published)
				{
					all = true;
					break;
				}
				var rendered = await db.RenderedPages.AsNoTracking()
					.Where(r => r.PageId == id)
					.Select(r => new { r.Title, r.Slug })
					.FirstOrDefaultAsync(ct);
				if (rendered is null || rendered.Title != page.Title || rendered.Slug != page.Slug)
				{
					all = true;
					break;
				}
			}
		}

		var menu = await services.GetRequiredService<MenuService>().GetPublicAsync(Menu.MainHandle, ct);
		var config = await services.GetRequiredService<SiteConfigService>().GetAsync(ct);
		var pages = services.GetRequiredService<PageService>();
		var baseUrl = options.Value.PublicBaseUrl.TrimEnd('/');

		var targets = await db.Pages.AsNoTracking()
			.Where(p => all ? p.Status == PageStatus.Published : ids.Contains(p.Id))
			.Select(p => new { p.Id, p.Slug })
			.ToListAsync(ct);

		foreach (var target in targets)
		{
			var page = await pages.GetPublishedBySlugAsync(target.Slug, ct);
			if (page is null) continue; // unpublished meanwhile; the queued All covers it
			var result = await renderer.RenderAsync(new RenderState(target.Slug, page, menu, config, baseUrl), ct);
			await StoreAsync(db, target.Id, page, result, startedAt, ct);
		}

		if (all)
		{
			await db.RenderedPages.Where(r => r.Page!.Status != PageStatus.Published).ExecuteDeleteAsync(ct);
			var notFound = await renderer.RenderAsync(new RenderState("not-found", null, menu, config, baseUrl), ct);
			status.NotFoundHtml = notFound.Html;
			status.CurrentVersion = notFound.Version;
		}
		logger.LogInformation("Rendered {Count} public page(s){All}.", targets.Count, all ? " (all)" : "");
	}

	private static async Task StoreAsync(
		AppDbContext db, Guid pageId, Models.Dto.PublicPage page, RenderResult result, DateTimeOffset renderedAt,
		CancellationToken ct)
	{
		var row = await db.RenderedPages.FirstOrDefaultAsync(r => r.PageId == pageId, ct);
		if (row is null)
		{
			row = new RenderedPage { PageId = pageId, Html = "", Title = "", Slug = "", RendererVersion = "" };
			db.RenderedPages.Add(row);
		}
		row.Html = result.Html;
		row.Title = page.Title;
		row.Slug = page.Slug;
		row.RendererVersion = result.Version;
		row.RenderedAt = renderedAt;
		try
		{
			await db.SaveChangesAsync(ct);
		}
		catch (DbUpdateException)
		{
			// page deleted mid-render (FK): its queued All cleans up
			db.ChangeTracker.Clear();
		}
	}
}
