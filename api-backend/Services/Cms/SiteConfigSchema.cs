using api_backend.Models.Site;

namespace api_backend.Services.Cms;

/// <summary>
/// Shape of the owner's site config (admin "Configuration"): fixed <see cref="Fields"/> (one value each) and
/// <see cref="Groups"/> (ordered key/value entries the owner adds, removes and reorders). Instance code reads the
/// values as <c>config.fields.firmName</c> / <c>config.groups.socials</c>.
/// </summary>
public sealed record SiteConfigSchema
{
	public List<ConfigFieldDef> Fields { get; init; } = [];
	public List<ConfigGroupDef> Groups { get; init; } = [];
}

public sealed record ConfigFieldDef
{
	/// <summary>camelCase identifier: instance code reads <c>config.fields.&lt;key&gt;</c>.</summary>
	public required string Key { get; init; }
	public required string Label { get; init; }
	/// <summary>Not <c>address</c> (group entries only).</summary>
	public ConfigFieldType Type { get; init; } = ConfigFieldType.Text;
	/// <summary>Can't be saved empty.</summary>
	public bool Required { get; init; }
}

public sealed record ConfigGroupDef
{
	/// <summary>camelCase identifier: instance code reads <c>config.groups.&lt;key&gt;</c>.</summary>
	public required string Key { get; init; }
	public required string Label { get; init; }
	/// <summary>Entries the owner picks from (key = the entry's key, type fixed).</summary>
	public List<ConfigPresetDef> Presets { get; init; } = [];
	/// <summary>Preset keys a fresh config starts with.</summary>
	public List<string> Defaults { get; init; } = [];
	/// <summary>Preset keys that are always there (can't be removed).</summary>
	public List<string> Required { get; init; } = [];
	/// <summary>Owner may add entries with their own name and type.</summary>
	public bool AllowCustom { get; init; } = true;

	public ConfigPresetDef? Preset(string key) => Presets.FirstOrDefault(p => p.Key == key);
}

public sealed record ConfigPresetDef
{
	public required string Key { get; init; }
	public required string Label { get; init; }
	public ConfigFieldType Type { get; init; } = ConfigFieldType.Text;
}

/// <summary>Site config every instance has: the firm name (page titles use it) and a contact group.</summary>
public static class CoreSiteConfig
{
	public const string FirmName = "firmName";
	public const string Contact = "contact";

	public static readonly List<ConfigFieldDef> Fields = [new() { Key = FirmName, Label = "Firm name" }];

	public static readonly List<ConfigGroupDef> Groups =
	[
		new()
		{
			Key = Contact,
			Label = "Contact",
			Presets =
			[
				new() { Key = "address", Label = "Address", Type = ConfigFieldType.Address },
				new() { Key = "phone", Label = "Phone", Type = ConfigFieldType.Phone },
				new() { Key = "email", Label = "Email", Type = ConfigFieldType.Email },
			],
			Defaults = ["address", "phone", "email"],
		},
	];
}
