using System.Text.Json;
using api_backend.Models.Pages;

namespace api_backend.Services.Cms;

/// <summary>
/// Pages of the default instance config (no <c>cms.config.json</c>, or one without <c>pages</c>): a Home, Privacy
/// Policy and Legal page with placeholder content the owner replaces; the last two are linked in the footer.
/// Seeding itself is <see cref="api_backend.Services.Pages.PageSeeder"/>.
/// </summary>
public static class DefaultPages
{
	/// <summary>Served at <c>/</c> by the frontend.</summary>
	public const string HomeSlug = "home";
	public const string PrivacySlug = "privacy-policy";
	public const string LegalSlug = "legal";

	public static List<CmsPageConfig> Create() =>
	[
		new()
		{
			Title = "Home",
			Slug = HomeSlug,
			Blocks =
			[
				Heading("Welcome"),
				Paragraph("This is your new website. Every page is built from blocks: headings, paragraphs, "
					+ "images, lists, cards and links."),
				Card("Getting started",
					"Log in and open Pages in the admin area to edit this page or add new ones.",
					[Link("Open the admin area", "/admin/pages")]),
			],
		},
		new()
		{
			Title = "Privacy Policy",
			Slug = PrivacySlug,
			Footer = true,
			Blocks =
			[
				Heading("Privacy Policy"),
				Paragraph("Explain here which personal data this website collects (e.g. contact forms, server "
					+ "logs, cookies), why and on what legal basis, how long it is kept, who it is shared with, "
					+ "and how visitors can exercise their rights (access, correction, deletion). Replace this "
					+ "text with your own privacy policy."),
			],
		},
		new()
		{
			Title = "Legal",
			Slug = LegalSlug,
			Footer = true,
			Blocks =
			[
				Heading("Legal Notice"),
				Paragraph("State here who is responsible for this website: name or company, postal address, "
					+ "contact details and, where required, registration and VAT numbers. Replace this text "
					+ "with your own legal notice (imprint)."),
			],
		},
	];

	// attribute shapes = the blocks' `attributes` defaults (frontend/src/lib/web-editor/blocks/*/index.ts)
	private static Block Heading(string text) => Make("heading", new()
	{
		["text"] = text,
		["level"] = 2,
		["blockWidth"] = "default",
		["backgroundColor"] = "",
		["textColor"] = "",
	});

	private static Block Paragraph(string text) => Make("paragraph", new()
	{
		["text"] = text,
		["alignment"] = "left",
		["blockWidth"] = "default",
		["backgroundColor"] = "",
		["textColor"] = "",
	});

	private static Block Card(string title, string description, List<Block> inner) => Make("card", new()
	{
		["title"] = title,
		["description"] = description,
		["mediaId"] = "",
		["blockWidth"] = "default",
		["backgroundColor"] = "",
		["textColor"] = "",
	}, inner);

	private static Block Link(string label, string url) => Make("link", new()
	{
		["label"] = label,
		["url"] = url,
		["textColor"] = "",
	});

	private static Block Make(string name, Dictionary<string, object> attributes, List<Block>? inner = null) => new()
	{
		Id = Guid.NewGuid().ToString("N")[..12],
		Name = name,
		Attributes = attributes.ToDictionary(a => a.Key, a => JsonSerializer.SerializeToElement(a.Value)),
		InnerBlocks = inner ?? [],
	};
}
