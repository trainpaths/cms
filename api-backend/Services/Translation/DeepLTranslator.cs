using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace api_backend.Services.Translation;

/// <summary>
/// DeepL API v2 (<c>POST /v2/translate</c>) as a typed HttpClient. Free keys (<c>:fx</c>) use api-free.deepl.com.
/// Batches of 50 texts (DeepL's limit), order kept; empty texts are never sent (no characters billed).
/// </summary>
public sealed class DeepLTranslator(HttpClient http, IOptions<TranslationOptions> options) : ITranslator
{
	public const int BatchSize = 50;

	private string ApiKey => options.Value.DeepL.ApiKey;

	public static Uri BaseUrlFor(string apiKey) =>
		new(apiKey.TrimEnd().EndsWith(":fx", StringComparison.Ordinal) ? "https://api-free.deepl.com/" : "https://api.deepl.com/");

	/// <summary>Source languages have no variants in DeepL: the primary subtag, upper-case.</summary>
	public static string SourceCode(string tag) => tag.Split('-')[0].ToUpperInvariant();

	/// <summary>English and Portuguese need a variant as target; everything else is the primary subtag.</summary>
	public static string TargetCode(string tag)
	{
		var parts = tag.Split('-');
		var code = parts[0].ToUpperInvariant();
		var region = parts.Length > 1 ? parts[1].ToUpperInvariant() : "";
		return code switch
		{
			"EN" => region == "GB" ? "EN-GB" : "EN-US",
			"PT" => region == "BR" ? "PT-BR" : "PT-PT",
			_ => code,
		};
	}

	private sealed record Request(
		[property: JsonPropertyName("text")] List<string> Text,
		[property: JsonPropertyName("source_lang")] string SourceLang,
		[property: JsonPropertyName("target_lang")] string TargetLang);

	private sealed record Response([property: JsonPropertyName("translations")] List<Translation> Translations);

	private sealed record Translation([property: JsonPropertyName("text")] string Text);

	public async Task<IReadOnlyList<string>> TranslateAsync(IReadOnlyList<string> texts, string sourceTag,
		string targetTag, CancellationToken ct)
	{
		var result = texts.ToArray();
		var pending = Enumerable.Range(0, texts.Count).Where(i => texts[i].Length > 0).ToList();
		// DeepL has no Swiss German; Swiss spelling has no ß
		var swiss = targetTag.Equals("de-CH", StringComparison.OrdinalIgnoreCase);
		foreach (var batch in pending.Chunk(BatchSize))
		{
			var translated = await PostAsync(new Request([.. batch.Select(i => texts[i])], SourceCode(sourceTag),
				TargetCode(targetTag)), ct);
			if (translated.Count != batch.Length)
				throw new TranslationException("DeepL returned an unexpected answer.");
			for (var j = 0; j < batch.Length; j++)
				result[batch[j]] = swiss ? translated[j].Text.Replace("ß", "ss") : translated[j].Text;
		}
		return result;
	}

	private async Task<List<Translation>> PostAsync(Request body, CancellationToken ct)
	{
		using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(BaseUrlFor(ApiKey), "v2/translate"))
		{
			Content = JsonContent.Create(body),
		};
		request.Headers.TryAddWithoutValidation("Authorization", $"DeepL-Auth-Key {ApiKey}");
		HttpResponseMessage response;
		try
		{
			response = await http.SendAsync(request, ct);
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
		{
			throw new TranslationException("DeepL is not reachable, try again.", inner: ex);
		}
		using (response)
		{
			if (response.IsSuccessStatusCode)
				return (await response.Content.ReadFromJsonAsync<Response>(ct))?.Translations
					?? throw new TranslationException("DeepL returned an unexpected answer.");
			throw (int)response.StatusCode switch
			{
				400 => new TranslationException($"DeepL can't translate into {body.TargetLang}.", badRequest: true),
				401 or 403 => new TranslationException("DeepL key rejected."),
				429 => new TranslationException("DeepL is rate limiting, try again."),
				456 => new TranslationException("DeepL quota exhausted."),
				>= 500 => new TranslationException("DeepL is unavailable, try again."),
				_ => new TranslationException($"DeepL answered {(int)response.StatusCode} {response.StatusCode}."),
			};
		}
	}
}
