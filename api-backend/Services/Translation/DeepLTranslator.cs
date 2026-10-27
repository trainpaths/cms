using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace api_backend.Services.Translation;

/// <summary>
/// DeepL API v2 (<c>POST /v2/translate</c>) as a typed HttpClient. Free keys (<c>:fx</c>) use api-free.deepl.com.
/// Batches of at most 50 texts (DeepL's limit) and <see cref="BatchBytes"/> of text (DeepL rejects bodies over 128 KiB),
/// up to <see cref="Parallelism"/> at a time; order kept; empty texts are never sent (no characters billed).
/// </summary>
public sealed class DeepLTranslator(HttpClient http, IOptions<TranslationOptions> options) : ITranslator
{
	public const int BatchSize = 50;
	/// <summary>UTF-8 text per request; leaves room for JSON escaping under DeepL's 128 KiB body limit.</summary>
	public const int BatchBytes = 100 * 1024;
	public const int Parallelism = 3;

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
		// DeepL has no Swiss German; Swiss spelling has no ß
		var swiss = targetTag.Equals("de-CH", StringComparison.OrdinalIgnoreCase);
		var (source, target) = (SourceCode(sourceTag), TargetCode(targetTag));
		await Parallel.ForEachAsync(Batches(texts), new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = ct },
			async (batch, token) =>
			{
				var translated = await PostAsync(new Request([.. batch.Select(i => texts[i])], source, target), token);
				if (translated.Count != batch.Count)
					throw new TranslationException("DeepL returned an unexpected answer.");
				for (var j = 0; j < batch.Count; j++)
					result[batch[j]] = swiss ? translated[j].Text.Replace("ß", "ss") : translated[j].Text;
			});
		return result;
	}

	/// <summary>Indexes of the non-empty texts, in order, cut at <see cref="BatchSize"/> texts or <see cref="BatchBytes"/>.</summary>
	public static List<List<int>> Batches(IReadOnlyList<string> texts)
	{
		var batches = new List<List<int>>();
		var (current, bytes) = (new List<int>(), 0);
		for (var i = 0; i < texts.Count; i++)
		{
			if (texts[i].Length == 0) continue;
			var size = Encoding.UTF8.GetByteCount(texts[i]);
			if (current.Count > 0 && (current.Count == BatchSize || bytes + size > BatchBytes))
				(current, bytes) = (Add(current), 0);
			current.Add(i);
			bytes += size;
		}
		if (current.Count > 0) batches.Add(current);
		return batches;

		List<int> Add(List<int> full)
		{
			batches.Add(full);
			return [];
		}
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
