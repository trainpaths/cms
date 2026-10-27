using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using api_backend.Services.Translation;
using AwesomeAssertions;
using Microsoft.Extensions.Options;

namespace api_backend.Tests.Unit.Services;

public class DeepLTranslatorTests
{
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	/// <summary>Answers every request with the texts upper-cased (or <see cref="Status"/>), recording what was sent.</summary>
	private sealed class FakeDeepL : HttpMessageHandler
	{
		public HttpStatusCode Status = HttpStatusCode.OK;
		public string Answer = "";
		public List<(Uri Uri, string? Auth, JsonObject Body)> Requests { get; } = [];

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
		{
			var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!.AsObject();
			// batches run in parallel
			lock (Requests)
				Requests.Add((request.RequestUri!, request.Headers.Authorization?.ToString()
					?? request.Headers.GetValues("Authorization").Single(), body));
			if (Status != HttpStatusCode.OK)
				return new HttpResponseMessage(Status);
			var texts = body["text"]!.AsArray().Select(t => (JsonNode)new JsonObject
			{
				["text"] = Answer.Length > 0 ? Answer : t!.GetValue<string>().ToUpperInvariant(),
			});
			var json = new JsonObject { ["translations"] = new JsonArray([.. texts]) }.ToJsonString();
			return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
		}
	}

	private static (DeepLTranslator, FakeDeepL) Create(string key = "secret")
	{
		var handler = new FakeDeepL();
		var options = Options.Create(new TranslationOptions { DeepL = new DeepLOptions { ApiKey = key } });
		return (new DeepLTranslator(new HttpClient(handler), options), handler);
	}

	[Fact]
	public async Task ProKey_UsesTheProHost_AndTheAuthHeader()
	{
		var (translator, deepl) = Create("pro-key");
		await translator.TranslateAsync(["Hello"], "en-US", "fr-CH", Ct);

		deepl.Requests.Single().Uri.ToString().Should().Be("https://api.deepl.com/v2/translate");
		deepl.Requests.Single().Auth.Should().Be("DeepL-Auth-Key pro-key");
		deepl.Requests.Single().Body["source_lang"]!.GetValue<string>().Should().Be("EN");
		deepl.Requests.Single().Body["target_lang"]!.GetValue<string>().Should().Be("FR");
	}

	[Fact]
	public async Task FreeKey_UsesTheFreeHost()
	{
		var (translator, deepl) = Create("abc:fx");
		await translator.TranslateAsync(["Hello"], "en", "de", Ct);
		deepl.Requests.Single().Uri.Host.Should().Be("api-free.deepl.com");
	}

	[Fact]
	public async Task Batches_KeepOrder_AndSkipEmptyTexts()
	{
		var (translator, deepl) = Create();
		var texts = Enumerable.Range(0, 120).Select(i => i % 10 == 0 ? "" : $"t{i}").ToList();

		var result = await translator.TranslateAsync(texts, "en", "it", Ct);

		result.Should().Equal(texts.Select(t => t.ToUpperInvariant()));
		deepl.Requests.Select(r => r.Body["text"]!.AsArray().Count).Should().BeEquivalentTo([50, 50, 8]); // 108 non-empty
	}

	[Fact]
	public void Batches_AlsoCutBySize()
	{
		// 30 KB each: three fit under BatchBytes, the fourth starts a new batch
		var texts = Enumerable.Range(0, 7).Select(_ => new string('x', 30 * 1024)).ToList();
		DeepLTranslator.Batches(texts).Select(b => b.Count).Should().Equal(3, 3, 1);
	}

	[Fact]
	public void Batches_OversizedTextGoesAlone()
	{
		var texts = new List<string> { "a", new('x', DeepLTranslator.BatchBytes + 1), "b" };
		DeepLTranslator.Batches(texts).Should().BeEquivalentTo(new List<List<int>> { new() { 0 }, new() { 1 }, new() { 2 } },
			o => o.WithStrictOrdering());
	}

	[Fact]
	public async Task ParallelBatches_KeepOrder()
	{
		var (translator, deepl) = Create();
		var texts = Enumerable.Range(0, 260).Select(i => $"t{i}").ToList();
		(await translator.TranslateAsync(texts, "en", "it", Ct)).Should().Equal(texts.Select(t => t.ToUpperInvariant()));
		deepl.Requests.Should().HaveCount(6);
	}

	[Fact]
	public async Task NothingToTranslate_SendsNothing()
	{
		var (translator, deepl) = Create();
		(await translator.TranslateAsync(["", ""], "en", "de", Ct)).Should().Equal("", "");
		deepl.Requests.Should().BeEmpty();
	}

	[Theory]
	[InlineData("en-GB", "EN-GB")]
	[InlineData("en-US", "EN-US")]
	[InlineData("en", "EN-US")]
	[InlineData("pt-BR", "PT-BR")]
	[InlineData("pt-PT", "PT-PT")]
	[InlineData("pt", "PT-PT")]
	[InlineData("de-CH", "DE")]
	public void TargetCodes(string tag, string expected) => DeepLTranslator.TargetCode(tag).Should().Be(expected);

	[Fact]
	public void SourceCodes_HaveNoVariant() => DeepLTranslator.SourceCode("en-GB").Should().Be("EN");

	[Fact]
	public async Task SwissGerman_ReplacesEszett_OnlyForDeCh()
	{
		var (translator, deepl) = Create();
		deepl.Answer = "Straße";
		(await translator.TranslateAsync(["street"], "en", "de-CH", Ct)).Should().Equal("Strasse");
		(await translator.TranslateAsync(["street"], "en", "de-DE", Ct)).Should().Equal("Straße");
	}

	[Theory]
	[InlineData(HttpStatusCode.Forbidden, "DeepL key rejected.", false)]
	[InlineData(HttpStatusCode.TooManyRequests, "DeepL is rate limiting, try again.", false)]
	[InlineData((HttpStatusCode)456, "DeepL quota exhausted.", false)]
	[InlineData(HttpStatusCode.BadRequest, "DeepL can't translate into XX.", true)]
	public async Task Errors_BecomeTranslationExceptions(HttpStatusCode status, string message, bool badRequest)
	{
		var (translator, deepl) = Create();
		deepl.Status = status;
		var act = () => translator.TranslateAsync(["x"], "en", "xx", Ct);
		var error = (await act.Should().ThrowAsync<TranslationException>()).Which;
		error.Message.Should().Be(message);
		error.BadRequest.Should().Be(badRequest);
	}
}
