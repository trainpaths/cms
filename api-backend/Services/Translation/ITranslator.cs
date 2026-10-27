namespace api_backend.Services.Translation;

/// <summary>Machine translation of editor text (provider seam; <see cref="DeepLTranslator"/> today).</summary>
public interface ITranslator
{
	/// <summary>
	/// <paramref name="texts"/> from <paramref name="sourceTag"/> into <paramref name="targetTag"/> (BCP 47 tags of
	/// the site config), in the same order. Empty strings come back empty.
	/// </summary>
	/// <exception cref="TranslationException">provider refused or unreachable; the message is for the user</exception>
	Task<IReadOnlyList<string>> TranslateAsync(IReadOnlyList<string> texts, string sourceTag, string targetTag,
		CancellationToken ct);
}

/// <summary>A translation request failed; <see cref="Exception.Message"/> is shown to the editor.</summary>
public class TranslationException(string message, bool badRequest = false, Exception? inner = null)
	: Exception(message, inner)
{
	/// <summary>The request itself can't be translated (unsupported language): 400 rather than 502.</summary>
	public bool BadRequest { get; } = badRequest;
}
