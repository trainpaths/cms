using System.Text.Json.Serialization;
using api_backend.Models.Pages;

namespace api_backend.Services.Cms;

/// <summary>
/// Instance config: <c>cms.config.json</c>, written by the developer of a client site (the instance repo) and baked
/// into its API image. The API is its only runtime reader; the admin gets what it needs from <c>/api/public/instance</c>
/// (<see cref="CmsSiteMeta.Lang"/> is applied at frontend build time).
/// Page flags (template, locked, editable, footer) are looked up by slug at read time: nothing is stored per page,
/// so removing a page from the config simply unlocks it. Keep in sync with <c>frontend/cms.config.schema.json</c>.
/// </summary>
public sealed record CmsConfig
{
	/// <summary>Editor hint (JSON schema path); ignored.</summary>
	[JsonPropertyName("$schema")]
	public string? Schema { get; init; }

	/// <summary>Customer accounts (register/login at <c>/login</c>). Off: the customer auth API answers 404.</summary>
	public bool PublicAuth { get; init; }

	/// <summary>Site-level meta of the public pages.</summary>
	public CmsSiteMeta Site { get; init; } = new();

	public CmsBlocksConfig Blocks { get; init; } = new();

	/// <summary>Pages the CMS seeds; omitted = <see cref="DefaultPages"/> (Home, Privacy Policy, Legal).</summary>
	public List<CmsPageConfig> Pages { get; init; } = DefaultPages.Create();

	/// <summary>The instance's own site config fields and groups, added to the core ones (<see cref="SiteConfigSchema"/>).</summary>
	public SiteConfigSchema SiteConfig { get; init; } = new();

	/// <summary>No config file: default pages, core site config, public auth off.</summary>
	public static CmsConfig Default { get; } = new();

	public CmsPageConfig? Page(string slug) => Pages.FirstOrDefault(p => p.Slug == slug);

	/// <summary>
	/// The effective site config shape: core field <c>firmName</c> and group <c>contact</c> first (unless the instance
	/// defines the same key, which replaces them), then the instance's own fields and groups.
	/// </summary>
	public SiteConfigSchema SiteConfigSchema() => new()
	{
		Fields = [.. CoreSiteConfig.Fields.Where(c => SiteConfig.Fields.All(f => f.Key != c.Key)), .. SiteConfig.Fields],
		Groups = [.. CoreSiteConfig.Groups.Where(c => SiteConfig.Groups.All(g => g.Key != c.Key)), .. SiteConfig.Groups],
	};
}

public sealed record CmsSiteMeta
{
	/// <summary>
	/// <c>&lt;html lang&gt;</c> of the site (BCP 47, e.g. <c>de</c>, <c>en-GB</c>). Applied at build time by the frontend's
	/// <c>cms()</c> Vite plugin (server/site-lang.js); the API only validates it, so a bad tag fails here too.
	/// </summary>
	public string Lang { get; init; } = "en";
}

public sealed record CmsBlocksConfig
{
	/// <summary>Built-in block names hidden from the editor's inserters (existing content still renders and edits).</summary>
	public List<string> Exclude { get; init; } = [];
}

public sealed record CmsPageConfig
{
	public required string Slug { get; init; }
	public required string Title { get; init; }

	/// <summary>Instance template (<c>src/templates/&lt;name&gt;.vue</c>) that renders the page; implies locked.</summary>
	public string? Template { get; init; }

	/// <summary>Slug fixed and page undeletable; seeded on every start when missing.</summary>
	public bool Locked { get; init; }

	/// <summary>False: the template renders everything, the owner can't edit blocks (requires a template).</summary>
	public bool Editable { get; init; } = true;

	/// <summary>Linked in the public footer while published, in config order.</summary>
	public bool Footer { get; init; }

	/// <summary>Initial content, seeded once.</summary>
	public List<Block> Blocks { get; init; } = [];

	[JsonIgnore]
	public bool IsLocked => Locked || Template is not null;
}
