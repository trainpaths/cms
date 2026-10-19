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

		(await service.GetPublishedBySlugAsync("visible", Ct)).Should().BeNull();

		var published = await service.UpdateAsync(page.Id, new UpdatePageRequest(null, null, null, Status: PageStatus.Published), StaffId, Ct);
		published.Value!.StatusChanged.Should().BeTrue();
		published.Value.Page.Status.Should().Be(PageStatus.Published);
		published.Value.Page.PublishedAt.Should().NotBeNull();
		(await service.GetPublishedBySlugAsync("visible", Ct)).Should().NotBeNull();

		await service.UpdateAsync(page.Id, new UpdatePageRequest(null, null, null, Status: PageStatus.Draft), StaffId, Ct);
		(await service.GetPublishedBySlugAsync("visible", Ct)).Should().BeNull();
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
		var live = (await service.GetPublishedBySlugAsync("live", Ct))!;
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
		(await service.GetPublishedBySlugAsync("live", Ct))!.Title.Should().Be("Edited");
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
		(await service.GetPublishedBySlugAsync("page", Ct)).Should().BeNull();

		await service.UpdateAsync(page.Id, new UpdatePageRequest(null, null, null, Status: PageStatus.Published), StaffId, Ct);
		(await service.GetPublishedBySlugAsync("page", Ct))!.Title.Should().Be("Changed");
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
