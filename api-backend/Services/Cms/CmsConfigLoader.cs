using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using api_backend.Models.Site;
using api_backend.Services.Pages;

namespace api_backend.Services.Cms;

/// <summary>Reads and validates <c>cms.config.json</c>; an invalid file stops the API on startup.</summary>
public static partial class CmsConfigLoader
{
	/// <summary>Config key for the file path, relative to the content root (<c>/app</c> in the image).</summary>
	public const string PathKey = "Cms:ConfigPath";
	public const string DefaultFileName = "cms.config.json";

	// strict: a typo in a key should fail loudly, not silently fall back to a default
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
	{
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
	};

	[GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
	private static partial Regex NamePattern();

	[GeneratedRegex("^[a-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$")]
	private static partial Regex LangPattern();

	// site config keys are read in code: config.fields.firmName
	[GeneratedRegex("^[a-z][a-zA-Z0-9]*$")]
	private static partial Regex IdentifierPattern();

	public static CmsConfig Load(IConfiguration config, IHostEnvironment env, ILogger logger)
	{
		var path = Path.Combine(env.ContentRootPath, config[PathKey] ?? DefaultFileName);
		if (!File.Exists(path))
		{
			logger.LogInformation("No instance config at {Path}, using defaults.", path);
			return CmsConfig.Default;
		}
		var cms = Parse(File.ReadAllText(path), path);
		logger.LogInformation("Instance config loaded from {Path}.", path);
		return cms;
	}

	public static CmsConfig Parse(string json, string source = DefaultFileName)
	{
		CmsConfig? cms;
		try
		{
			cms = JsonSerializer.Deserialize<CmsConfig>(json, Json);
		}
		catch (JsonException e)
		{
			throw new InvalidOperationException($"{source}: {e.Message}", e);
		}
		if (cms is null)
			throw new InvalidOperationException($"{source}: expected a JSON object.");

		var errors = Validate(cms);
		if (errors.Count > 0)
			throw new InvalidOperationException($"{source} is invalid:\n- {string.Join("\n- ", errors)}");
		return cms;
	}

	public static List<string> Validate(CmsConfig cms)
	{
		var errors = new List<string>();
		var slugs = new HashSet<string>();
		for (var i = 0; i < cms.Pages.Count; i++)
		{
			var page = cms.Pages[i];
			var at = $"pages[{i}] ({page.Slug})";
			if (PageService.ValidateSlug(page.Slug) is { } slugError)
				errors.Add($"{at}: {slugError}");
			if (!slugs.Add(page.Slug))
				errors.Add($"{at}: duplicate slug.");
			if (string.IsNullOrWhiteSpace(page.Title) || page.Title.Length > 200)
				errors.Add($"{at}: title must be 1-200 characters.");
			if (page.Template is not null && !NamePattern().IsMatch(page.Template))
				errors.Add($"{at}: template must be kebab-case (the file name in src/templates/).");
			if (!page.Editable && page.Template is null)
				errors.Add($"{at}: \"editable\": false needs a template (nothing else would render the page).");
			if (PageService.ValidateBlocks(page.Blocks) is { } blockError)
				errors.Add($"{at}: {blockError}");
		}
		if (!LangPattern().IsMatch(cms.Site.Lang))
			errors.Add($"site.lang: '{cms.Site.Lang}' is not a language tag (e.g. en, de, en-GB).");
		foreach (var name in cms.Blocks.Exclude.Where(n => !NamePattern().IsMatch(n)))
			errors.Add($"blocks.exclude: '{name}' is not a block name.");
		ValidateSiteConfig(cms.SiteConfig, errors);
		return errors;
	}

	private static void ValidateSiteConfig(SiteConfigSchema schema, List<string> errors)
	{
		void Key(string at, string key, HashSet<string> seen)
		{
			if (!IdentifierPattern().IsMatch(key) || key.Length > ConfigLimits.MaxKeyLength)
				errors.Add($"{at}: key '{key}' must be a camelCase identifier (letters and digits, starting lowercase).");
			if (!seen.Add(key))
				errors.Add($"{at}: duplicate key '{key}'.");
		}
		void Label(string at, string label)
		{
			if (string.IsNullOrWhiteSpace(label) || label.Length > 100)
				errors.Add($"{at}: label must be 1-100 characters.");
		}

		var fieldKeys = new HashSet<string>();
		for (var i = 0; i < schema.Fields.Count; i++)
		{
			var field = schema.Fields[i];
			var at = $"siteConfig.fields[{i}]";
			Key(at, field.Key, fieldKeys);
			Label(at, field.Label);
			if (field.Type == ConfigFieldType.Address)
				errors.Add($"{at}: type address is for group entries only.");
		}

		var groupKeys = new HashSet<string>();
		for (var i = 0; i < schema.Groups.Count; i++)
		{
			var group = schema.Groups[i];
			var at = $"siteConfig.groups[{i}]";
			Key(at, group.Key, groupKeys);
			Label(at, group.Label);
			var presetKeys = new HashSet<string>();
			for (var j = 0; j < group.Presets.Count; j++)
			{
				Key($"{at}.presets[{j}]", group.Presets[j].Key, presetKeys);
				Label($"{at}.presets[{j}]", group.Presets[j].Label);
			}
			foreach (var key in group.Defaults.Concat(group.Required).Where(k => !presetKeys.Contains(k)).Distinct())
				errors.Add($"{at}: '{key}' in defaults/required is not one of its presets.");
			if (group.Presets.Count == 0 && !group.AllowCustom)
				errors.Add($"{at}: no presets and allowCustom false: nothing could be added.");
		}
	}
}
