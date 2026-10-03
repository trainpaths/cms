using System.Text.Json.Serialization;
using api_backend.Models.Auth;
using api_backend.Models.Media;

namespace api_backend.Models.Site;

/// <summary>
/// Site-wide info the owner maintains (single row, <see cref="SingletonId"/>). Its shape comes from the instance
/// config (<c>CmsConfig.SiteConfigSchema()</c>): a few fixed fields (firm name...) and groups of key/value entries
/// (contact, socials...); <see cref="Values"/> holds what the owner entered. Logo and icon stay columns because the
/// public site itself consumes them (header/footer, favicon).
/// </summary>
public class SiteConfig
{
	public const int SingletonId = 1;

	public int Id { get; set; } = SingletonId;

	public SiteConfigValues Values { get; set; } = new();

	public Guid? LogoMediaId { get; set; }
	public MediaAsset? Logo { get; set; }
	public Guid? IconMediaId { get; set; }
	public MediaAsset? Icon { get; set; }
	/// <summary>Link-preview image (og:image) of pages without an image of their own.</summary>
	public Guid? ShareImageMediaId { get; set; }
	public MediaAsset? ShareImage { get; set; }

	public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
	public Guid? UpdatedById { get; set; }
	public Staff? UpdatedBy { get; set; }
}

/// <summary>Stored owner values (jsonb): fixed fields by key, group entries by group key, in the owner's order.</summary>
public class SiteConfigValues
{
	public Dictionary<string, string> Fields { get; set; } = [];
	public Dictionary<string, List<ConfigEntryValue>> Groups { get; set; } = [];
}

/// <summary>
/// One group entry as stored and as sent by the admin. <see cref="Key"/> is a preset key of the group (type fixed by
/// the preset) or, for a custom entry, the owner's own name for it. <see cref="Address"/> is set for type
/// <c>address</c> only (then <see cref="Value"/> is empty).
/// </summary>
public record ConfigEntryValue(string Id, string Key, ConfigFieldType Type, string Value, ConfigAddress? Address);

public record ConfigAddress(string Street, string PostalCode, string City, string Country);

// lowercase on the wire, in the jsonb column, in cms.config.json and in OpenAPI; unknown values fail binding (400)
[JsonConverter(typeof(JsonStringEnumConverter<ConfigFieldType>))]
public enum ConfigFieldType
{
	[JsonStringEnumMemberName("text")] Text,
	[JsonStringEnumMemberName("email")] Email,
	[JsonStringEnumMemberName("phone")] Phone,
	[JsonStringEnumMemberName("link")] Link,
	/// <summary>Street, postal code, city, country; group entries only.</summary>
	[JsonStringEnumMemberName("address")] Address,
}

public static class ConfigLimits
{
	public const int MaxEntries = 50;
	public const int MaxIdLength = 64;
	public const int MaxKeyLength = 50;
	public const int MaxValueLength = 1000;
	public const int MaxAddressPartLength = 200;
}
