namespace api_backend.Services.Translation;

public class TranslationOptions
{
	public const string SectionName = "Translation";

	public DeepLOptions DeepL { get; set; } = new();

	/// <summary>Machine translation is offered (a DeepL key is set).</summary>
	public bool Enabled => !string.IsNullOrWhiteSpace(DeepL.ApiKey);
}

public class DeepLOptions
{
	/// <summary>DeepL API key (<c>DEEPL_API_KEY</c>); empty = machine translation off. Keys ending in <c>:fx</c> are free-plan keys.</summary>
	public string ApiKey { get; set; } = "";
}
