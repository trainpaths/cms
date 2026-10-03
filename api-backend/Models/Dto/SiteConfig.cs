using api_backend.Models.Site;

namespace api_backend.Models.Dto;

/// <summary>
/// Site config as served to the admin form and the public site, shaped by the instance config's schema: every field
/// (empty when not filled) and every group (its entries in the owner's order, labels resolved). Logo/icon/share image
/// are resolved media. Not owner-edited: <see cref="FooterLinks"/> (published pages marked <c>footer</c> in the instance
/// config).
/// </summary>
public record SiteConfigResponse(
	Dictionary<string, string> Fields,
	Dictionary<string, List<ConfigEntry>> Groups,
	MediaRef? Logo,
	MediaRef? Icon,
	MediaRef? ShareImage,
	DateTimeOffset? UpdatedAt,
	List<FooterLink> FooterLinks);

/// <summary>A group entry; <see cref="Label"/> = the preset's label, or the key for a custom entry.</summary>
public record ConfigEntry(string Id, string Key, string Label, ConfigFieldType Type, string Value, ConfigAddress? Address);

public record FooterLink(string Title, string Slug);

/// <summary>
/// Full replace: fields by key, each group's entries in order (entries without an id get one); null media ids clear.
/// Validated against the instance config's schema.
/// </summary>
public record UpdateSiteConfigRequest(
	Dictionary<string, string>? Fields,
	Dictionary<string, List<ConfigEntryValue>>? Groups,
	Guid? LogoMediaId,
	Guid? IconMediaId,
	Guid? ShareImageMediaId = null);
