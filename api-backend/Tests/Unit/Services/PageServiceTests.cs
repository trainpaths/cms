using System.Text.Json;
using api_backend.Models.Dto;
using api_backend.Models.Pages;
using api_backend.Services.Cms;
using api_backend.Services.Pages;
using AwesomeAssertions;

namespace api_backend.Tests.Unit.Services;

public class PageServiceTests
{
	private static readonly Guid StaffId = Guid.NewGuid();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private static Block Block(string name, Dictionary<string, JsonElement>? attributes = null, params Block[] inner) =>
		new() { Id = Guid.NewGuid().ToString("N")[..10], Name = name, Attributes = attributes ?? new(), InnerBlocks = inner.ToList() };

	private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

	[Theory]
	[InlineData("Hello World", "hello-world")]
	[InlineData("  Über   Café!! ", "uber-cafe")]
	[InlineData("---", "page")]
	public void Slugify_ProducesKebabCase(string input, string expected) =>
		PageService.Slugify(input).Should().Be(expected);

	[Theory]
	[InlineData("about-us", true)]
	[InlineData("About", false)]
	[InlineData("a--b", false)]
	[InlineData("-a", false)]
	[InlineData("login", false)]
	[InlineData("admin", false)]
	[InlineData("user", false)]
	[InlineData("profile", true)]
	public void ValidateSlug_EnforcesFormatAndReservedWords(string slug, bool valid) =>
		(PageService.ValidateSlug(slug) is null).Should().Be(valid);

	[Fact]
	public async Task Create_WithoutSlug_GeneratesUniqueSlugFromTitle()
	{
		await using var db = TestDbContextFactory.Create();
		var service = new PageService(db, CmsConfig.Default);

		var first = await service.CreateAsync(new CreatePageRequest("About Us", null, null), StaffId, Ct);
		var second = await service.CreateAsync(new CreatePageRequest("About Us", null, null), StaffId, Ct);

		first.Value!.Slug.Should().Be("about-us");
		second.Value!.Slug.Should().Be("about-us-2");
		first.Value.Status.Should().Be(PageStatus.Draft);
	}

	[Fact]
	public async Task Create_WithReservedTitle_SuffixesGeneratedSlug()
	{
		await using var db = TestDbContextFactory.Create();
		var result = await new PageService(db, CmsConfig.Default).CreateAsync(new CreatePageRequest("Login", null, null), StaffId, Ct);

		result.Value!.Slug.Should().Be("login-page");
	}

	private static readonly CmsConfig Localized = CmsConfig.Default with
	{
		Site = new CmsSiteMeta { Lang = new CmsSiteLang { Default = "en-US", Localization = ["de-CH", "fr-CH"] } },
	};

	[Theory]
	[InlineData("de")]
	[InlineData("en")]
	public async Task LanguageCodes_AreReservedSlugs_OnALocalizedSite(string code)
	{
		await using var db = TestDbContextFactory.Create();
		var service = new PageService(db, Localized);

		var create = await service.CreateAsync(new CreatePageRequest("x", code, null), StaffId, Ct);
		create.Error.Should().Be(PageError.Invalid);
		create.Detail.Should().Be($"Slug '{code}' is reserved.");

		var page = await service.CreateAsync(new CreatePageRequest("Page", null, null), StaffId, Ct);
		(await service.UpdateAsync(page.Value!.Id, new UpdatePageRequest(null, code, null), StaffId, Ct))
			.Error.Should().Be(PageError.Invalid);
		// generated from a title
		(await service.CreateAsync(new CreatePageRequest(code.ToUpperInvariant(), null, null), StaffId, Ct))
			.Value!.Slug.Should().Be($"{code}-page");
	}

	[Fact]
	public async Task LanguageCodes_AreOrdinarySlugs_OnASingleLanguageSite()
	{
		await using var db = TestDbContextFactory.Create();
		var result = await new PageService(db, CmsConfig.Default).CreateAsync(new CreatePageRequest("x", "de", null), StaffId, Ct);

		result.Value!.Slug.Should().Be("de");
	}

	private static PageService.LivePage Live(List<Block> blocks, Dictionary<string, PageTextTranslation>? translations = null) =>
		new(Guid.NewGuid(), "about", "About", "Meta", "Desc", blocks, translations ?? [], null);

	[Fact]
	public void Localize_OverlaysTranslatedAttributes_FallsBack_AndStripsTranslations()
	{
		var paragraph = Block("paragraph", new() { ["text"] = Json("\"Hello\""), ["alignment"] = Json("\"left\"") });
		paragraph.Translations = new()
		{
			["de"] = new() { ["text"] = Json("\"Hallo\"") },
			["fr"] = new() { ["text"] = Json("\"\"") },
		};
		var heading = Block("heading", new() { ["text"] = Json("\"Title\""), ["level"] = Json("2") });
		heading.Translations = new() { ["de"] = new() { ["level"] = Json("3") } };
		var group = Block("group", null, heading);
		var page = Live([paragraph, group], new() { ["de"] = new PageTextTranslation("Über uns", "", "Beschreibung") });

		var de = PageService.Localize(page, "de", "en");
		de.Title.Should().Be("Über uns");
		de.MetaTitle.Should().Be("Meta"); // empty → default
		de.MetaDescription.Should().Be("Beschreibung");
		de.Blocks[0].Attributes["text"].GetString().Should().Be("Hallo");
		de.Blocks[0].Attributes["alignment"].GetString().Should().Be("left");
		de.Blocks[1].InnerBlocks[0].Attributes["level"].GetInt32().Should().Be(3); // non-string primitive
		de.Blocks.Should().AllSatisfy(b => b.Translations.Should().BeNull());
		de.Translations.Should().BeEmpty();

		PageService.Localize(page, "fr", "en").Blocks[0].Attributes["text"].GetString().Should().Be("Hello");
		var en = PageService.Localize(page, "en", "en");
		en.Title.Should().Be("About");
		en.Blocks[0].Attributes["text"].GetString().Should().Be("Hello");
		en.Blocks[0].Translations.Should().BeNull();
		// source untouched (cached live pages are shared)
		paragraph.Translations.Should().NotBeNull();
		paragraph.Attributes["text"].GetString().Should().Be("Hello");
	}

	[Theory]
	[InlineData("German")]
	[InlineData("d")]
	public void ValidateBlocks_RejectsBadLanguageCodes(string code)
	{
		var block = Block("paragraph");
		block.Translations = new() { [code] = new() { ["text"] = Json("\"x\"") } };
		PageService.ValidateBlocks([block]).Should().Contain("language code");
	}

	[Fact]
	public void ValidateBlocks_RejectsNonPrimitiveTranslations_AndDropsEmptyOnes()
	{
		var bad = Block("paragraph");
		bad.Translations = new() { ["de"] = new() { ["text"] = Json("[1]") } };
		PageService.ValidateBlocks([bad]).Should().Contain("must be a string");

		var empty = Block("paragraph");
		empty.Translations = new() { ["de"] = new(), ["xx"] = new() { ["text"] = Json("\"kept\"") } };
		PageService.ValidateBlocks([empty]).Should().BeNull();
		empty.Translations.Should().ContainKey("xx").And.NotContainKey("de"); // unknown codes are kept

		var none = Block("paragraph");
		none.Translations = new() { ["de"] = new() };
		PageService.ValidateBlocks([none]).Should().BeNull();
		none.Translations.Should().BeNull();
	}

	[Fact]
	public void ValidateBlocks_TranslationSources()
	{
		var badCode = Block("paragraph");
		badCode.TranslationSources = new() { ["German"] = new() { ["text"] = "1a2b3c4d" } };
		PageService.ValidateBlocks([badCode]).Should().Contain("language code");

		var tooLong = Block("paragraph");
		tooLong.TranslationSources = new() { ["de"] = new() { ["text"] = new string('a', PageService.MaxSourceHashLength + 1) } };
		PageService.ValidateBlocks([tooLong]).Should().Contain("translation sources");

		var empty = Block("paragraph");
		empty.TranslationSources = new() { ["de"] = new() };
		PageService.ValidateBlocks([empty]).Should().BeNull();
		empty.TranslationSources.Should().BeNull();
	}

	[Fact]
	public void Localize_StripsTranslationSources()
	{
		var paragraph = Block("paragraph", new() { ["text"] = Json("\"Hello\"") });
		paragraph.Translations = new() { ["de"] = new() { ["text"] = Json("\"Hallo\"") } };
		paragraph.TranslationSources = new() { ["de"] = new() { ["text"] = "1a2b3c4d" } };
		var page = Live([paragraph], new() { ["de"] = new PageTextTranslation("Über uns", Sources: new PageTextSources("abc")) });

		var de = PageService.Localize(page, "de", "en");
		de.Blocks[0].TranslationSources.Should().BeNull();
		de.Translations.Should().BeEmpty();
		JsonSerializer.Serialize(de.Blocks).Should().NotContain("1a2b3c4d");
	}

	[Fact]
	public async Task PageTranslationSources_KeptNextToTranslatedFields_CountAsChanges()
	{
		await using var db = TestDbContextFactory.Create();
		var service = new PageService(db, CmsConfig.Default);
		var created = (await service.CreateAsync(new CreatePageRequest("Page", null, null), StaffId, Ct)).Value!;
		(await service.UpdateAsync(created.Id, new UpdatePageRequest(null, null, null,
			Translations: new() { ["de"] = new PageTextTranslation("Seite", Sources: new PageTextSources(new string('a', 17))) }),
			StaffId, Ct)).Detail.Should().Contain("Translation sources (de)");

		await service.UpdateAsync(created.Id, new UpdatePageRequest(null, null, null,
			Translations: new() { ["de"] = new PageTextTranslation("Seite", Sources: new PageTextSources("aaaa", "bbbb")) }),
			StaffId, Ct);
		await service.UpdateAsync(created.Id, new UpdatePageRequest(null, null, null, Status: PageStatus.Published), StaffId, Ct);
		var detail = (await service.GetAsync(created.Id, Ct))!;
		detail.Translations["de"].Sources.Should().Be(new PageTextSources("aaaa")); // no meta title translation: no source

		// a source-only change is an unpublished change (the snapshot keeps them too)
		var changed = await service.UpdateAsync(created.Id, new UpdatePageRequest(null, null, null,
			Translations: new() { ["de"] = new PageTextTranslation("Seite", Sources: new PageTextSources("cccc")) }), StaffId, Ct);
		changed.Value!.Page.HasUnpublishedChanges.Should().BeTrue();

		// sources alone are no translation
		(await service.UpdateAsync(created.Id, new UpdatePageRequest(null, null, null,
			Translations: new() { ["fr"] = new PageTextTranslation(Sources: new PageTextSources("dddd")) }), StaffId, Ct))
			.Value.Should().NotBeNull();
		(await service.GetAsync(created.Id, Ct))!.Translations.Should().NotContainKey("fr");
	}

	[Fact]
	public async Task Translations_Validation_Snapshot_Duplicate_Import()
	{
		await using var db = TestDbContextFactory.Create();
		var service = new PageService(db, CmsConfig.Default);
		var created = (await service.CreateAsync(new CreatePageRequest("Page", null, null), StaffId, Ct)).Value!;

		(await service.UpdateAsync(created.Id, new UpdatePageRequest(null, null, null,
			Translations: new() { ["Deutsch"] = new PageTextTranslation("x") }), StaffId, Ct)).Error.Should().Be(PageError.Invalid);
		(await service.UpdateAsync(created.Id, new UpdatePageRequest(null, null, null,
			Translations: new() { ["de"] = new PageTextTranslation("", new string('x', 71)) }), StaffId, Ct))
			.Detail.Should().Contain("Meta title (de)");

		await service.UpdateAsync(created.Id, new UpdatePageRequest(null, null, null, Status: PageStatus.Published), StaffId, Ct);
		var edited = await service.UpdateAsync(created.Id, new UpdatePageRequest(null, null, null,
			Translations: new() { ["de"] = new PageTextTranslation(" Seite "), ["fr"] = new PageTextTranslation() }), StaffId, Ct);
		edited.Value!.Page.HasUnpublishedChanges.Should().BeTrue();
		edited.Value.LiveRenamed.Should().BeFalse(); // working copy only
		var published = await service.UpdateAsync(created.Id, new UpdatePageRequest(null, null, null, Status: PageStatus.Published), StaffId, Ct);
		published.Value!.Page.HasUnpublishedChanges.Should().BeFalse();
		published.Value.LiveRenamed.Should().BeTrue(); // the German live title changed

		var detail = (await service.GetAsync(created.Id, Ct))!;
		detail.Translations.Should().ContainSingle().Which.Value.Title.Should().Be("Seite"); // trimmed, empty fr dropped

		var copy = (await service.DuplicateAsync(created.Id, StaffId, Ct)).Value!;
		copy.Translations["de"].Title.Should().Be("Seite");

		var imported = (await service.ImportAsync(new ImportPageRequest("Imported", null, [], null, null, null,
			new() { ["it"] = new PageTextTranslation("Importato") }), StaffId, Ct)).Value!;
		imported.Page.Translations["it"].Title.Should().Be("Importato");
		// v1 export files have no translations
		(await service.ImportAsync(new ImportPageRequest("Old", null, [], null, null, null), StaffId, Ct))
			.Value!.Page.Translations.Should().BeEmpty();
	}

	[Fact]
	public async Task Create_WithTakenSlug_ReturnsSlugTaken()
	{
		await using var db = TestDbContextFactory.Create();
		var service = new PageService(db, CmsConfig.Default);
		await service.CreateAsync(new CreatePageRequest("One", "home", null), StaffId, Ct);

		var result = await service.CreateAsync(new CreatePageRequest("Two", "home", null), StaffId, Ct);

		result.Error.Should().Be(PageError.SlugTaken);
	}

	[Fact]
	public async Task Update_IsPartial_AndKeepsOmittedFields()
	{
		await using var db = TestDbContextFactory.Create();
		var service = new PageService(db, CmsConfig.Default);
		var created = await service.CreateAsync(
			new CreatePageRequest("Page", null, [Block("paragraph", new() { ["text"] = Json("\"hi\"") })]), StaffId, Ct);

		var updated = await service.UpdateAsync(created.Value!.Id, new UpdatePageRequest("Renamed", null, null), StaffId, Ct);

		updated.Value!.LiveRenamed.Should().BeFalse(); // a draft: nothing live to rename
		updated.Value.Page.Title.Should().Be("Renamed");
		updated.Value.Page.Slug.Should().Be("page");
		updated.Value.Page.BlockCount.Should().Be(1);
	}

	[Fact]
	public async Task Update_UnknownId_ReturnsNotFound()
	{
		await using var db = TestDbContextFactory.Create();
		var result = await new PageService(db, CmsConfig.Default).UpdateAsync(Guid.NewGuid(), new UpdatePageRequest("x", null, null), StaffId, Ct);

		result.Error.Should().Be(PageError.NotFound);
	}

	[Fact]
	public async Task PublishAndUnpublish_ControlPublicVisibility()
	{
		await using var db = TestDbContextFactory.Create();
		var service = new PageService(db, CmsConfig.Default);
		var page = (await service.CreateAsync(new CreatePageRequest("Visible", null, null), StaffId, Ct)).Value!;

		(await service.GetPublishedBySlugAsync("visible", null, Ct)).Should().BeNull();

		var published = await service.UpdateAsync(page.Id, new UpdatePageRequest(null, null, null, Status: PageStatus.Published), StaffId, Ct);
		published.Value!.StatusChanged.Should().BeTrue();
		published.Value.Page.Status.Should().Be(PageStatus.Published);
		published.Value.Page.PublishedAt.Should().NotBeNull();
		(await service.GetPublishedBySlugAsync("visible", null, Ct)).Should().NotBeNull();

		await service.UpdateAsync(page.Id, new UpdatePageRequest(null, null, null, Status: PageStatus.Draft), StaffId, Ct);
		(await service.GetPublishedBySlugAsync("visible", null, Ct)).Should().BeNull();
	}

	[Fact]
	public async Task Save_OnAPublishedPage_StaysOffTheLivePage_UntilPublished()
	{
		await using var db = TestDbContextFactory.Create();
		var service = new PageService(db, CmsConfig.Default);
		var paragraph = Block("paragraph");
		var page = (await service.CreateAsync(new CreatePageRequest("Live", null, [paragraph]), StaffId, Ct)).Value!;
		await service.UpdateAsync(page.Id, new UpdatePageRequest(null, null, null, Status: PageStatus.Published), StaffId, Ct);

		var saved = await service.UpdateAsync(page.Id,
			new UpdatePageRequest("Edited", null, [paragraph, Block("heading")], MetaTitle: "Meta"), StaffId, Ct);
		saved.Value!.Should().Be(saved.Value with { StatusChanged = false, LiveChanged = false, LiveRenamed = false });
		saved.Value.Page.HasUnpublishedChanges.Should().BeTrue();
		var live = (await service.GetPublishedBySlugAsync("live", null, Ct))!;
		live.Title.Should().Be("Live");
		live.MetaTitle.Should().BeEmpty();
		live.Blocks.Should().ContainSingle();
		(await service.GetAsync(page.Id, Ct))!.Title.Should().Be("Edited");

		// back to the live content = nothing to publish
		var reverted = await service.UpdateAsync(page.Id,
			new UpdatePageRequest("Live", null, [paragraph], MetaTitle: ""), StaffId, Ct);
		reverted.Value!.Page.HasUnpublishedChanges.Should().BeFalse();

		await service.UpdateAsync(page.Id, new UpdatePageRequest("Edited", null, null), StaffId, Ct);
		var published = await service.UpdateAsync(page.Id, new UpdatePageRequest(null, null, null, Status: PageStatus.Published), StaffId, Ct);
		published.Value!.Should().Be(published.Value with { StatusChanged = false, LiveChanged = true, LiveRenamed = true });
		published.Value.Page.HasUnpublishedChanges.Should().BeFalse();
		(await service.GetPublishedBySlugAsync("live", null, Ct))!.Title.Should().Be("Edited");
	}

	[Fact]
	public async Task Unpublish_HidesThePage_AndRepublishShowsTheSavedCopy()
	{
		await using var db = TestDbContextFactory.Create();
		var service = new PageService(db, CmsConfig.Default);
		var page = (await service.CreateAsync(new CreatePageRequest("Page", null, null), StaffId, Ct)).Value!;
		await service.UpdateAsync(page.Id, new UpdatePageRequest(null, null, null, Status: PageStatus.Published), StaffId, Ct);
		await service.UpdateAsync(page.Id, new UpdatePageRequest("Changed", null, null), StaffId, Ct);

		var unpublished = await service.UpdateAsync(page.Id, new UpdatePageRequest(null, null, null, Status: PageStatus.Draft), StaffId, Ct);
		unpublished.Value!.StatusChanged.Should().BeTrue();
		unpublished.Value.Page.HasUnpublishedChanges.Should().BeFalse();
		(await service.GetPublishedBySlugAsync("page", null, Ct)).Should().BeNull();

		await service.UpdateAsync(page.Id, new UpdatePageRequest(null, null, null, Status: PageStatus.Published), StaffId, Ct);
		(await service.GetPublishedBySlugAsync("page", null, Ct))!.Title.Should().Be("Changed");
	}

	[Fact]
	public void ValidateBlocks_RejectsNonPrimitiveAttributes() =>
		PageService.ValidateBlocks([Block("card", new() { ["nested"] = Json("{\"a\":1}") })])
			.Should().Contain("nested");

	[Fact]
	public void ValidateBlocks_RejectsInvalidName() =>
		PageService.ValidateBlocks([Block("Bad Name")]).Should().NotBeNull();

	[Fact]
	public void ValidateBlocks_RejectsTooDeepNesting()
	{
		var block = Block("card");
		var root = block;
		for (var i = 0; i < PageService.MaxDepth; i++)
		{
			var child = Block("card");
			block.InnerBlocks.Add(child);
			block = child;
		}

		PageService.ValidateBlocks([root]).Should().Contain("nested");
	}

	[Fact]
	public void ValidateBlocks_AcceptsNestedPrimitiveTree() =>
		PageService.ValidateBlocks([
			Block("card", new() { ["title"] = Json("\"Hi\""), ["wide"] = Json("true") },
				Block("link", new() { ["label"] = Json("\"x\""), ["level"] = Json("2") })),
		]).Should().BeNull();
}
