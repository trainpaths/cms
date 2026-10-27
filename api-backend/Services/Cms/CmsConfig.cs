using System.Text.Json;
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

	public CmsThemeConfig Theme { get; init; } = new();

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
	/// Languages of the site: <c>"de-CH"</c> (one language) or <c>{ "default": "en-US", "localization": ["de-CH"] }</c>.
	/// Applied at build time by the frontend's <c>cms()</c> Vite plugin (server/site-lang.js: <c>&lt;html lang&gt;</c>,
	/// UI languages); the API only validates it, so a bad tag fails here too.
	/// </summary>
	public CmsSiteLang Lang { get; init; } = new();
}

/// <summary>
/// <see cref="Default"/> + <see cref="Localization"/> are BCP 47 tags (<c>de-CH</c>); a language's <b>code</b> is the
/// primary subtag (<c>de</c>), unique per site: URL prefix, <c>cms_lang</c> cookie, key of stored translations.
/// No localization = a single-language site, exactly as before.
/// </summary>
[JsonConverter(typeof(CmsSiteLangConverter))]
public sealed record CmsSiteLang
{
	public string Default { get; init; } = "en";
	public List<string> Localization { get; init; } = [];

	/// <summary>Default first.</summary>
	[JsonIgnore]
	public IEnumerable<string> Tags => [Default, .. Localization];

	[JsonIgnore]
	public bool IsLocalized => Localization.Count > 0;

	[JsonIgnore]
	public string DefaultCode => CodeOf(Default);

	/// <summary>Language codes, default first (single-language: just the default).</summary>
	[JsonIgnore]
	public IReadOnlyList<string> Codes => [.. Tags.Select(CodeOf)];

	/// <summary>A configured non-default code: the URL prefix of that language.</summary>
	public bool IsPrefix(string code) => IsLocalized && code != DefaultCode && Codes.Contains(code);

	/// <summary>Configured code, else the default (unknown/missing <c>?lang=</c>).</summary>
	public string Resolve(string? code) => code is not null && Codes.Contains(code) ? code : DefaultCode;

	public static string CodeOf(string tag) => tag.Split('-')[0];
}

/// <summary>Reads <c>site.lang</c> as a tag string (one language) or as the object form.</summary>
public sealed class CmsSiteLangConverter : JsonConverter<CmsSiteLang>
{
	// the object form without this converter (it would recurse); the caller's options keep unknown keys an error
	private sealed record Form
	{
		public string Default { get; init; } = "en";
		public List<string> Localization { get; init; } = [];
	}

	public override CmsSiteLang Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.String)
			return new CmsSiteLang { Default = reader.GetString()! };
		if (reader.TokenType != JsonTokenType.StartObject)
			throw new JsonException("site.lang must be a language tag or { \"default\", \"localization\" }.");
		var form = JsonSerializer.Deserialize<Form>(ref reader, options)!;
		return new CmsSiteLang { Default = form.Default, Localization = form.Localization };
	}

	public override void Write(Utf8JsonWriter writer, CmsSiteLang value, JsonSerializerOptions options) =>
		JsonSerializer.Serialize(writer, new Form { Default = value.Default, Localization = value.Localization }, options);
}

public sealed record CmsThemeConfig
{
	/// <summary>
	/// Site palette, key → CSS colour (<c>"primary": "#111111"</c>). Applied at frontend build time by the <c>cms()</c>
	/// Vite plugin (server/theme.js: <c>.site-theme { --color-primary: #111111 }</c>, the editor's colour swatches); the
	/// API only validates it. Blocks store the key, so changing a value recolours existing content.
	/// </summary>
	public Dictionary<string, string> Colors { get; init; } = [];
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
