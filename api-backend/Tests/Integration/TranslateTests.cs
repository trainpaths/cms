using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using api_backend.Models.Auth;
using api_backend.Models.Dto;
using api_backend.Services.Auth.JWT;
using api_backend.Services.Cms;
using api_backend.Services.Translation;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace api_backend.Tests.Integration;

/// <summary><c>POST /api/translate</c> with a fake translator (no DeepL calls in tests).</summary>
public class TranslateTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private sealed class Upper : ITranslator
	{
		public List<(string Source, string Target)> Calls { get; } = [];

		public Task<IReadOnlyList<string>> TranslateAsync(IReadOnlyList<string> texts, string sourceTag,
			string targetTag, CancellationToken ct)
		{
			Calls.Add((sourceTag, targetTag));
			if (targetTag == "it-CH") throw new TranslationException("DeepL quota exhausted.");
			return Task.FromResult<IReadOnlyList<string>>([.. texts.Select(t => t.ToUpperInvariant())]);
		}
	}

	private static readonly CmsConfig Localized = ApiFactory.Cms with
	{
		Site = new CmsSiteMeta { Lang = new CmsSiteLang { Default = "en-US", Localization = ["de-CH", "it-CH"] } },
	};

	private WebApplicationFactory<Program> App(Upper translator, bool enabled = true) => factory.WithWebHostBuilder(b =>
	{
		if (enabled)
			b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Translation:DeepL:ApiKey"] = "test-key",
			}));
		b.ConfigureTestServices(s =>
		{
			s.RemoveAll<CmsConfig>();
			s.AddSingleton(Localized);
			s.RemoveAll<ITranslator>();
			s.AddSingleton<ITranslator>(translator);
		});
	});

	private static async Task<string> TokenAsync(WebApplicationFactory<Program> app, string userType)
	{
		await using var scope = app.Services.CreateAsyncScope();
		var jwt = app.Services.GetRequiredService<JwtTokenService>();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		var email = $"tr-{Guid.NewGuid()}@test.com";
		if (userType == "staff")
		{
			var staff = new Staff { Email = email, PasswordHash = "x" };
			db.Staff.Add(staff);
			await db.SaveChangesAsync(Ct);
			return jwt.CreateAccessToken(staff.Id, email, "staff", ["staff"]).Token;
		}
		var customer = new Customer { Email = email, PasswordHash = "x" };
		db.Customers.Add(customer);
		await db.SaveChangesAsync(Ct);
		return jwt.CreateAccessToken(customer.Id, email, "customer", []).Token;
	}

	private static Task<HttpResponseMessage> PostAsync(HttpClient client, string? token, object body)
	{
		var request = new HttpRequestMessage(HttpMethod.Post, "/api/translate") { Content = JsonContent.Create(body) };
		if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		return client.SendAsync(request, Ct);
	}

	[Fact]
	public async Task Staff_GetsTranslations_FromTheDefaultLanguage()
	{
		var translator = new Upper();
		await using var app = App(translator);
		using var client = app.CreateCookielessClient();

		var response = await PostAsync(client, await TokenAsync(app, "staff"), new { target = "de", texts = new[] { "hello", "" } });

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		(await response.Content.ReadFromJsonAsync<TranslateResponse>(Ct))!.Texts.Should().Equal("HELLO", "");
		translator.Calls.Should().Equal(("en-US", "de-CH"));
		(await client.GetFromJsonAsync<InstanceConfig>("/api/public/instance", Ct))!.MachineTranslation.Should().BeTrue();
	}

	[Fact]
	public async Task Anonymous401_Customer403()
	{
		await using var app = App(new Upper());
		using var client = app.CreateCookielessClient();
		var body = new { target = "de", texts = new[] { "x" } };
		(await PostAsync(client, null, body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
		(await PostAsync(client, await TokenAsync(app, "customer"), body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task WithoutKey_404_AndInstanceSaysOff()
	{
		await using var app = App(new Upper(), enabled: false);
		using var client = app.CreateCookielessClient();
		(await PostAsync(client, await TokenAsync(app, "staff"), new { target = "de", texts = new[] { "x" } }))
			.StatusCode.Should().Be(HttpStatusCode.NotFound);
		(await client.GetFromJsonAsync<InstanceConfig>("/api/public/instance", Ct))!.MachineTranslation.Should().BeFalse();
	}

	[Theory]
	[InlineData("en")] // default
	[InlineData("fr")] // not configured
	public async Task Target_MustBeAConfiguredNonDefaultLanguage(string target)
	{
		await using var app = App(new Upper());
		using var client = app.CreateCookielessClient();
		(await PostAsync(client, await TokenAsync(app, "staff"), new { target, texts = new[] { "x" } }))
			.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task TooManyOrTooLongTexts_400()
	{
		await using var app = App(new Upper());
		using var client = app.CreateCookielessClient();
		var token = await TokenAsync(app, "staff");
		(await PostAsync(client, token, new { target = "de", texts = Enumerable.Repeat("x", 501) }))
			.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		(await PostAsync(client, token, new { target = "de", texts = new[] { new string('x', 10_001) } }))
			.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task ProviderFailure_502_WithTheMessage()
	{
		await using var app = App(new Upper());
		using var client = app.CreateCookielessClient();
		var response = await PostAsync(client, await TokenAsync(app, "staff"), new { target = "it", texts = new[] { "x" } });
		response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
		(await response.Content.ReadAsStringAsync(Ct)).Should().Contain("DeepL quota exhausted.");
	}
}
