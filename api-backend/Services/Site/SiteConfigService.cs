using System.Net.Mail;
using api_backend.Models.Dto;
using api_backend.Models.Pages;
using api_backend.Models.Site;
using api_backend.Services.Cms;
using api_backend.Services.Media;
using Microsoft.EntityFrameworkCore;

namespace api_backend.Services.Site;

public record SiteConfigResult(SiteConfigResponse? Value, string? Error = null)
{
	public static SiteConfigResult Ok(SiteConfigResponse value) => new(value);
	public static SiteConfigResult Invalid(string detail) => new(null, detail);
}

/// <summary>
/// Reads and replaces the singleton <see cref="SiteConfig"/> row (created on first save). Its shape is the instance
/// config's <see cref="SiteConfigSchema"/>: reads fit stored values to it (the schema may have changed since they
/// were saved), writes are validated against it.
/// </summary>
public class SiteConfigService(AppDbContext db, CmsConfig cms)
{
	public async Task<SiteConfigResponse> GetAsync(CancellationToken ct)
	{
		var config = await db.SiteConfig.AsNoTracking()
			.Include(m => m.Logo)
			.Include(m => m.Icon)
			.Include(m => m.ShareImage)
			.FirstOrDefaultAsync(m => m.Id == SiteConfig.SingletonId, ct);
		var (fields, groups) = Resolve(cms.SiteConfigSchema(), config?.Values ?? new SiteConfigValues());
		return new SiteConfigResponse(fields, groups, ToRef(config?.Logo), ToRef(config?.Icon), ToRef(config?.ShareImage),
			config?.UpdatedAt, await FooterLinksAsync(ct));
	}

	/// <summary>Published pages marked <c>footer</c> in the instance config, in config order.</summary>
	private async Task<List<FooterLink>> FooterLinksAsync(CancellationToken ct)
	{
		var slugs = cms.Pages.Where(p => p.Footer).Select(p => p.Slug).ToList();
		if (slugs.Count == 0) return [];
		var pages = await db.Pages.AsNoTracking()
			.Where(p => slugs.Contains(p.Slug) && p.Status == PageStatus.Published)
			.Select(p => new FooterLink(p.Title, p.Slug))
			.ToListAsync(ct);
		return pages.OrderBy(l => slugs.IndexOf(l.Slug)).ToList();
	}

	/// <summary>
	/// Stored values fitted to the schema: every field (empty when missing), every group (its stored entries, or its
	/// default entries if never saved), required entries always present, unknown keys and disallowed custom entries
	/// dropped, preset labels and types applied.
	/// </summary>
	public static (Dictionary<string, string> Fields, Dictionary<string, List<ConfigEntry>> Groups) Resolve(
		SiteConfigSchema schema, SiteConfigValues values)
	{
		var fields = schema.Fields.ToDictionary(f => f.Key, f => values.Fields.GetValueOrDefault(f.Key) ?? "");
		var groups = new Dictionary<string, List<ConfigEntry>>();
		foreach (var group in schema.Groups)
		{
			var entries = values.Groups.TryGetValue(group.Key, out var stored)
				? stored.Where(e => group.Preset(e.Key) is not null || group.AllowCustom).ToList()
				: group.Defaults.Select(k => EmptyEntry(group.Preset(k)!)).ToList();
			entries.AddRange(group.Required.Where(k => entries.All(e => e.Key != k)).Select(k => EmptyEntry(group.Preset(k)!)));
			groups[group.Key] = entries.Select(e => ToEntry(group, e)).ToList();
		}
		return (fields, groups);
	}

	// default/required entries use the preset key as id: stable across reads until the owner saves
	private static ConfigEntryValue EmptyEntry(ConfigPresetDef preset) =>
		new(preset.Key, preset.Key, preset.Type, "", preset.Type == ConfigFieldType.Address ? EmptyAddress : null);

	private static readonly ConfigAddress EmptyAddress = new("", "", "", "");

	private static ConfigEntry ToEntry(ConfigGroupDef group, ConfigEntryValue e)
	{
		var preset = group.Preset(e.Key);
		var type = preset?.Type ?? e.Type;
		return type == ConfigFieldType.Address
			? new ConfigEntry(e.Id, e.Key, preset?.Label ?? e.Key, type, "", e.Address ?? EmptyAddress)
			: new ConfigEntry(e.Id, e.Key, preset?.Label ?? e.Key, type, e.Value, null);
	}

	public async Task<SiteConfigResult> UpdateAsync(UpdateSiteConfigRequest req, Guid staffId, CancellationToken ct)
	{
		var (values, error) = Normalize(cms.SiteConfigSchema(), req);
		if (error is not null)
			return SiteConfigResult.Invalid(error);

		foreach (var (id, label) in new[] { (req.LogoMediaId, "Logo"), (req.IconMediaId, "Icon"), (req.ShareImageMediaId, "Share image") })
			if (id is { } mediaId && !await db.MediaAssets.AnyAsync(m => m.Id == mediaId, ct))
				return SiteConfigResult.Invalid($"{label} media not found.");

		var config = await db.SiteConfig.FirstOrDefaultAsync(m => m.Id == SiteConfig.SingletonId, ct);
		if (config is null)
		{
			config = new SiteConfig();
			db.SiteConfig.Add(config);
		}
		config.Values = values!;
		config.LogoMediaId = req.LogoMediaId;
		config.IconMediaId = req.IconMediaId;
		config.ShareImageMediaId = req.ShareImageMediaId;
		config.UpdatedAt = DateTimeOffset.UtcNow;
		config.UpdatedById = staffId;
		await db.SaveChangesAsync(ct);

		return SiteConfigResult.Ok(await GetAsync(ct));
	}

	/// <summary>Validates a full replace against the schema; strings trimmed, entry types taken from their preset.</summary>
	public static (SiteConfigValues? Values, string? Error) Normalize(SiteConfigSchema schema, UpdateSiteConfigRequest req)
	{
		var values = new SiteConfigValues();

		var input = req.Fields ?? [];
		if (input.Keys.FirstOrDefault(k => schema.Fields.All(f => f.Key != k)) is { } unknownField)
			return (null, $"Unknown field \"{unknownField}\".");
		foreach (var field in schema.Fields)
		{
			var value = input.GetValueOrDefault(field.Key)?.Trim() ?? "";
			if (field.Required && value.Length == 0)
				return (null, $"\"{field.Label}\" is required.");
			if (CheckValue(field.Label, field.Type, value) is { } fieldError)
				return (null, fieldError);
			values.Fields[field.Key] = value;
		}

		var groups = req.Groups ?? [];
		if (groups.Keys.FirstOrDefault(k => schema.Groups.All(g => g.Key != k)) is { } unknownGroup)
			return (null, $"Unknown group \"{unknownGroup}\".");
		foreach (var group in schema.Groups)
		{
			var (entries, groupError) = NormalizeGroup(group, groups.GetValueOrDefault(group.Key) ?? []);
			if (groupError is not null)
				return (null, groupError);
			values.Groups[group.Key] = entries!;
		}
		return (values, null);
	}

	private static (List<ConfigEntryValue>? Entries, string? Error) NormalizeGroup(ConfigGroupDef group, List<ConfigEntryValue> input)
	{
		if (input.Count > ConfigLimits.MaxEntries)
			return (null, $"{group.Label}: at most {ConfigLimits.MaxEntries} entries.");

		var entries = new List<ConfigEntryValue>();
		var ids = new HashSet<string>();
		var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var raw in input)
		{
			if (raw is null)
				return (null, $"{group.Label}: entry must not be null.");
			var key = raw.Key?.Trim() ?? "";
			var preset = group.Preset(key);
			var name = $"{group.Label}: \"{preset?.Label ?? key}\"";
			if (key.Length is 0 or > ConfigLimits.MaxKeyLength)
				return (null, $"{group.Label}: entry names must be 1-{ConfigLimits.MaxKeyLength} characters.");
			if (preset is null && !group.AllowCustom)
				return (null, $"{name} is not one of the entries this group offers.");
			if (!keys.Add(key))
				return (null, $"{name} appears twice.");
			if (!Enum.IsDefined(raw.Type))
				return (null, $"{name} has an unknown type.");

			var id = raw.Id?.Trim() ?? "";
			if (id.Length == 0)
				id = Guid.NewGuid().ToString("N")[..12];
			if (id.Length > ConfigLimits.MaxIdLength || !ids.Add(id))
				return (null, $"{name} has an invalid id.");

			var type = preset?.Type ?? raw.Type;
			if (type == ConfigFieldType.Address)
			{
				var a = raw.Address ?? EmptyAddress;
				var address = new ConfigAddress(a.Street?.Trim() ?? "", a.PostalCode?.Trim() ?? "", a.City?.Trim() ?? "",
					a.Country?.Trim() ?? "");
				if (new[] { address.Street, address.PostalCode, address.City, address.Country }
					.Any(part => part.Length > ConfigLimits.MaxAddressPartLength))
					return (null, $"{name}: address lines are limited to {ConfigLimits.MaxAddressPartLength} characters.");
				entries.Add(new ConfigEntryValue(id, key, type, "", address));
			}
			else
			{
				var value = raw.Value?.Trim() ?? "";
				if (CheckValue(name, type, value) is { } valueError)
					return (null, valueError);
				entries.Add(new ConfigEntryValue(id, key, type, value, null));
			}
		}

		if (group.Required.FirstOrDefault(k => !keys.Contains(k)) is { } missing)
			return (null, $"{group.Label}: \"{group.Preset(missing)!.Label}\" can't be removed.");
		return (entries, null);
	}

	// empty values allowed: the owner may keep an entry to fill in later
	private static string? CheckValue(string name, ConfigFieldType type, string value)
	{
		if (value.Length > ConfigLimits.MaxValueLength)
			return $"{name} exceeds {ConfigLimits.MaxValueLength} characters.";
		if (value.Length == 0)
			return null;
		if (type == ConfigFieldType.Link && !IsHttpUrl(value))
			return $"{name} must be an http(s) URL.";
		if (type == ConfigFieldType.Email && !IsEmail(value))
			return $"{name} must be an email address.";
		return null;
	}

	// absolute http(s) only: rendered as href, so no javascript:/data:
	public static bool IsHttpUrl(string value) =>
		Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

	private static bool IsEmail(string value) =>
		MailAddress.TryCreate(value, out var address) && address.Address == value;

	private static MediaRef? ToRef(Models.Media.MediaAsset? m) => m is null ? null : MediaService.ToRef(m);
}
