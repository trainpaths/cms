using api_backend.Services.Cms;

namespace api_backend.Models.Dto;

/// <summary>
/// The parts of the instance config (<c>cms.config.json</c>) the frontend needs; <see cref="SiteConfig"/> is the
/// effective site config shape (core + instance fields and groups) the admin form renders. <see cref="MachineTranslation"/>:
/// a DeepL key is set (the editor offers "Translate missing fields"; the key itself is never exposed).
/// </summary>
public record InstanceConfig(bool PublicAuth, List<string> ExcludedBlocks, SiteConfigSchema SiteConfig,
	bool MachineTranslation);
