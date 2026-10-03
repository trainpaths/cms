using api_backend.Models.Site;
using api_backend.Services.Cms;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;

namespace api_backend.Tests.Unit.Services;

public class CmsConfigLoaderTests
{
	[Fact]
	public void Parse_EmptyObject_UsesDefaults()
	{
		var cms = CmsConfigLoader.Parse("{}");

		cms.PublicAuth.Should().BeFalse();
		cms.Site.Lang.Should().Be("en");
		cms.Blocks.Exclude.Should().BeEmpty();
		cms.Pages.Select(p => p.Slug).Should().Equal(DefaultPages.HomeSlug, DefaultPages.PrivacySlug, DefaultPages.LegalSlug);
		cms.Pages.Where(p => p.Footer).Select(p => p.Slug).Should().Equal(DefaultPages.PrivacySlug, DefaultPages.LegalSlug);
	}

	[Fact]
	public void Parse_ReadsPagesAndFlags_AllowingSchemaCommentsAndTrailingCommas()
	{
		var cms = CmsConfigLoader.Parse("""
			{
				"$schema": "./node_modules/@trainpaths/cms/cms.config.schema.json",
				// developer comment
				"publicAuth": true,
				"site": { "lang": "de-AT" },
				"blocks": { "exclude": ["list", "list-item"] },
				"pages": [
					{ "slug": "imprint", "title": "Imprint", "template": "imprint", "editable": false, "footer": true },
					{ "slug": "about", "title": "About",
					  "blocks": [{ "id": "h1", "name": "heading", "attributes": { "text": "Hi", "level": 2 }, "innerBlocks": [] }] },
				],
			}
			""");

		cms.PublicAuth.Should().BeTrue();
		cms.Site.Lang.Should().Be("de-AT");
		cms.Blocks.Exclude.Should().Equal("list", "list-item");
		var imprint = cms.Page("imprint")!;
		imprint.IsLocked.Should().BeTrue("a template page's slug is what the site's code relies on");
		imprint.Editable.Should().BeFalse();
		imprint.Footer.Should().BeTrue();
		cms.Page("about")!.IsLocked.Should().BeFalse();
		cms.Page("about")!.Blocks.Single().Attributes["level"].GetInt32().Should().Be(2);
		cms.Page("missing").Should().BeNull();
	}

	[Fact]
	public void SiteConfigSchema_PutsCoreFirst_AndAnInstanceDefinitionReplacesTheCoreOne()
	{
		CmsConfig.Default.SiteConfigSchema().Fields.Select(f => f.Key).Should().Equal(CoreSiteConfig.FirmName);
		CmsConfig.Default.SiteConfigSchema().Groups.Select(g => g.Key).Should().Equal(CoreSiteConfig.Contact);

		var schema = CmsConfigLoader.Parse("""
			{
				"siteConfig": {
					"fields": [{ "key": "vatId", "label": "VAT ID" }],
					"groups": [
						{ "key": "socials", "label": "Social media", "presets": [{ "key": "instagram", "label": "Instagram", "type": "link" }] },
						{ "key": "contact", "label": "Reach us", "presets": [{ "key": "email", "label": "Email", "type": "email" }], "required": ["email"] }
					]
				}
			}
			""").SiteConfigSchema();

		schema.Fields.Select(f => f.Key).Should().Equal(CoreSiteConfig.FirmName, "vatId");
		schema.Groups.Select(g => g.Key).Should().Equal("socials", CoreSiteConfig.Contact);
		schema.Groups[1].Label.Should().Be("Reach us");
		schema.Groups[0].Preset("instagram")!.Type.Should().Be(ConfigFieldType.Link);
		schema.Groups[0].AllowCustom.Should().BeTrue();
	}

	[Fact]
	public void Parse_EmptyPages_SeedsNothing() =>
		CmsConfigLoader.Parse("""{ "pages": [] }""").Pages.Should().BeEmpty();

	[Theory]
	[InlineData("""{ "publicAuht": true }""", "publicAuht")]
	[InlineData("""{ "pages": [{ "title": "No slug" }] }""", "slug")]
	[InlineData("""{ "pages": [{ "slug": "x", "title": "X", "lockd": true }] }""", "lockd")]
	public void Parse_UnknownOrMissingKeys_Throw(string json, string mentioned) =>
		FluentActions.Invoking(() => CmsConfigLoader.Parse(json))
			.Should().Throw<InvalidOperationException>().WithMessage($"*{mentioned}*");

	[Theory]
	[InlineData("""{ "pages": [{ "slug": "login", "title": "X" }] }""", "*reserved*")]
	[InlineData("""{ "pages": [{ "slug": "a", "title": "A" }, { "slug": "a", "title": "B" }] }""", "*duplicate slug*")]
	[InlineData("""{ "pages": [{ "slug": "a", "title": "" }] }""", "*title*")]
	[InlineData("""{ "pages": [{ "slug": "a", "title": "A", "template": "Legal Page" }] }""", "*template*")]
	[InlineData("""{ "pages": [{ "slug": "a", "title": "A", "editable": false }] }""", "*needs a template*")]
	[InlineData("""{ "pages": [{ "slug": "a", "title": "A", "blocks": [{ "id": "", "name": "x" }] }] }""", "*pages[0] (a)*")]
	[InlineData("""{ "blocks": { "exclude": ["List"] } }""", "*blocks.exclude*")]
	[InlineData("""{ "site": { "lang": "German" } }""", "*site.lang*")]
	[InlineData("""{ "siteConfig": { "fields": [{ "key": "firm-name", "label": "X" }] } }""", "*camelCase*")]
	[InlineData("""{ "siteConfig": { "fields": [{ "key": "office", "label": "X", "type": "address" }] } }""", "*group entries only*")]
	[InlineData("""{ "siteConfig": { "groups": [{ "key": "a", "label": "A" }, { "key": "a", "label": "B" }] } }""", "*duplicate key*")]
	[InlineData("""{ "siteConfig": { "groups": [{ "key": "socials", "label": "S", "defaults": ["x"] }] } }""", "*not one of its presets*")]
	[InlineData("""{ "siteConfig": { "groups": [{ "key": "socials", "label": "S", "allowCustom": false }] } }""", "*nothing could be added*")]
	public void Parse_InvalidValues_ThrowWithTheProblem(string json, string message) =>
		FluentActions.Invoking(() => CmsConfigLoader.Parse(json))
			.Should().Throw<InvalidOperationException>().WithMessage(message);

	[Fact]
	public void Load_ReadsTheFileFromTheContentRoot_OrFallsBackToDefaults()
	{
		var dir = Directory.CreateTempSubdirectory();
		try
		{
			var env = new HostingEnvironment { ContentRootPath = dir.FullName };
			var config = new ConfigurationBuilder().Build();

			CmsConfigLoader.Load(config, env, NullLogger.Instance).Should().BeSameAs(CmsConfig.Default);

			File.WriteAllText(Path.Combine(dir.FullName, CmsConfigLoader.DefaultFileName), """{ "publicAuth": true }""");
			CmsConfigLoader.Load(config, env, NullLogger.Instance).PublicAuth.Should().BeTrue();
		}
		finally
		{
			dir.Delete(true);
		}
	}
}
