using System.Text.Json;
using api_backend.Models.Pages;
using api_backend.Services.Cms;
using Microsoft.EntityFrameworkCore;

namespace api_backend.Services.Pages;

/// <summary>
/// Seeds the pages of the instance config (<see cref="CmsConfig.Pages"/>), published. A fresh install (empty pages
/// table) gets all of them, so pages the owner deletes stay deleted; locked/template pages are re-created on every
/// start when missing (the site's code relies on them). <c>Bootstrap:SeedPages=false</c> turns the fresh-install
/// seed off (tests). Also warns about pages a newly configured language code made unreachable.
/// </summary>
public static class PageSeeder
{
	public static async Task EnsureAsync(IServiceProvider services, CancellationToken ct = default)
	{
		using var scope = services.CreateScope();
		var sp = scope.ServiceProvider;
		var cms = sp.GetRequiredService<CmsConfig>();
		var db = sp.GetRequiredService<AppDbContext>();

		var seedAll = sp.GetRequiredService<IConfiguration>().GetValue("Bootstrap:SeedPages", true)
			&& !await db.Pages.AnyAsync(ct);
		var existing = seedAll ? [] : (await db.Pages.Select(p => p.Slug).ToListAsync(ct)).ToHashSet();
		var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(PageSeeder));
		// /de is the German home now; renaming is left to the owner
		if (cms.Site.Lang.IsLocalized && existing.Intersect(cms.Site.Lang.Codes).ToList() is { Count: > 0 } shadowed)
			logger.LogWarning("Page slug(s) {Slugs} equal a language code and are unreachable; rename them.",
				string.Join(", ", shadowed));
		var missing = cms.Pages.Where(p => seedAll || (p.IsLocked && !existing.Contains(p.Slug))).ToList();
		if (missing.Count == 0) return;

		var now = DateTimeOffset.UtcNow;
		db.Pages.AddRange(missing.Select(p => new Page
		{
			Title = p.Title,
			Slug = p.Slug,
			Status = PageStatus.Published,
			// copies: the config's block lists are shared (singleton)
			Blocks = JsonSerializer.Deserialize<List<Block>>(JsonSerializer.Serialize(p.Blocks))!,
			BlockCount = PageService.CountBlocks(p.Blocks),
			PublishedTitle = p.Title,
			PublishedBlocks = JsonSerializer.Deserialize<List<Block>>(JsonSerializer.Serialize(p.Blocks))!,
			PublishedAt = now,
		}));
		await db.SaveChangesAsync(ct);

		logger.LogInformation("Seeded pages: {Slugs}.", string.Join(", ", missing.Select(p => p.Slug)));
	}
}
