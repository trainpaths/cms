using System.Text.Json;
using api_backend.Models.Pages;
using api_backend.Services.Cms;
using Microsoft.EntityFrameworkCore;

namespace api_backend.Services.Pages;

/// <summary>
/// Seeds the pages of the instance config (<see cref="CmsConfig.Pages"/>), published. A fresh install (empty pages
/// table) gets all of them, so pages the owner deletes stay deleted; locked/template pages are re-created on every
/// start when missing (the site's code relies on them). <c>Bootstrap:SeedPages=false</c> turns the fresh-install
/// seed off (tests).
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
		var missing = cms.Pages.Where(p => seedAll || (p.IsLocked && !existing.Contains(p.Slug))).ToList();
		if (missing.Count == 0) return;

		var now = DateTimeOffset.UtcNow;
		db.Pages.AddRange(missing.Select(p => new Page
		{
			Title = p.Title,
			Slug = p.Slug,
			Status = PageStatus.Published,
			// copy: the config's block lists are shared (singleton)
			Blocks = JsonSerializer.Deserialize<List<Block>>(JsonSerializer.Serialize(p.Blocks))!,
			PublishedAt = now,
		}));
		await db.SaveChangesAsync(ct);

		sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(PageSeeder))
			.LogInformation("Seeded pages: {Slugs}.", string.Join(", ", missing.Select(p => p.Slug)));
	}
}
