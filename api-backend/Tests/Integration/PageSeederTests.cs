using System.Net;
using System.Net.Http.Json;
using api_backend.Models.Dto;
using api_backend.Models.Pages;
using api_backend.Services.Cms;
using AwesomeAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace api_backend.Tests.Integration;

/// <summary>Own container (class fixture): the seeder only runs against an empty pages table.</summary>
public class PageSeederTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	// ApiFactory disables seeding; app config added here comes later and wins
	private WebApplicationFactory<Program> Seeding() => factory.WithWebHostBuilder(b => b
		.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
			new Dictionary<string, string?> { ["Bootstrap:SeedPages"] = "true" })));

	[Fact]
	public async Task Startup_SeedsPublishedPagesOnce_AndFooterLinksFollowPublishState()
	{
		for (var i = 0; i < 2; i++)
		{
			await using var app = Seeding();
			using var client = app.CreateCookielessClient();
			(await client.GetAsync("/api/public/pages/home", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
		}

		await using (var app = Seeding())
		{
			using var scope = app.Services.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
			var pages = await db.Pages.AsNoTracking().ToListAsync(Ct);
			pages.Select(p => p.Slug).Should().BeEquivalentTo(
				[DefaultPages.HomeSlug, DefaultPages.PrivacySlug, DefaultPages.LegalSlug]);
			pages.Should().OnlyContain(p => p.Status == PageStatus.Published && p.Blocks.Count > 0);

			using var client = app.CreateCookielessClient();
			var config = await client.GetFromJsonAsync<SiteConfigResponse>("/api/public/site-config", Ct);
			config!.FooterLinks.Should().Equal(
				new FooterLink("Privacy Policy", DefaultPages.PrivacySlug),
				new FooterLink("Legal", DefaultPages.LegalSlug));

			await db.Pages.Where(p => p.Slug == DefaultPages.PrivacySlug)
				.ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PageStatus.Draft), Ct);
			config = await client.GetFromJsonAsync<SiteConfigResponse>("/api/public/site-config", Ct);
			config!.FooterLinks.Should().Equal(new FooterLink("Legal", DefaultPages.LegalSlug));
		}
	}
}
