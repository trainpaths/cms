using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using api_backend.Models.Auth;
using api_backend.Models.Dto;
using api_backend.Models.Site;
using api_backend.Services.Auth.JWT;
using api_backend.Services.Cms;
using AwesomeAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace api_backend.Tests.Integration;

/// <summary>The row is a singleton shared by all tests in this class: each test PUTs its own full state.</summary>
public class SiteConfigTests : IClassFixture<ApiFactory>
{
	private const string Base = "/api/site-config";
	private const string PublicBase = "/api/public/site-config";
	private readonly ApiFactory _factory;
	private readonly HttpClient _client;
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52];

	public SiteConfigTests(ApiFactory factory)
	{
		_factory = factory;
		_client = factory.CreateCookielessClient();
	}

	[Fact]
	public async Task Get_Anonymous_ReturnsUnauthorized()
	{
		var response = await _client.GetAsync(Base, Ct);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Put_AsCustomer_ReturnsForbidden()
	{
		var token = CreateToken(Guid.NewGuid(), "customer", []);
		var response = await SendAsync(HttpMethod.Put, Base, token, Body(new(), new()));
		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Get_WithoutRow_ReturnsTheCoreSchemaWithDefaultEntries()
	{
		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
			await db.SiteConfig.ExecuteDeleteAsync(Ct);
		}

		var config = await _client.GetFromJsonAsync<SiteConfigResponse>(PublicBase, Ct);
		config!.Fields.Should().Equal(new Dictionary<string, string> { [CoreSiteConfig.FirmName] = "" });
		var contact = config.Groups.Should().ContainKey(CoreSiteConfig.Contact).WhoseValue;
		contact.Select(e => (e.Id, e.Key, e.Label, e.Type)).Should().Equal(
			("address", "address", "Address", ConfigFieldType.Address),
			("phone", "phone", "Phone", ConfigFieldType.Phone),
			("email", "email", "Email", ConfigFieldType.Email));
		contact[0].Address.Should().Be(new ConfigAddress("", "", "", ""));
		contact[1].Address.Should().BeNull();
		config.Logo.Should().BeNull();
		config.Icon.Should().BeNull();
		config.UpdatedAt.Should().BeNull();
	}

	[Fact]
	public async Task Put_RoundTrip_KeepsOrderTrimsAndResolvesMedia()
	{
		var token = await CreateStaffTokenAsync();
		var logo = await UploadAsync(token, "Our logo");
		var icon = await UploadAsync(token);

		var body = Body(
			new JsonObject { ["firmName"] = " ACME GmbH " },
			Contact(
				Entry("email", " hello@acme.test ", "email", id: "e1"),
				Address(" Main St 1 ", "12345", "Town", "Germany"),
				Entry("phone", "+49 123 456", "text", id: "p1"),
				Entry("Fax", "", "phone")),
			logo.Id, icon.Id);
		var response = await SendAsync(HttpMethod.Put, Base, token, body);
		response.StatusCode.Should().Be(HttpStatusCode.OK);

		var saved = (await response.Content.ReadFromJsonAsync<SiteConfigResponse>(Ct))!;
		saved.Fields[CoreSiteConfig.FirmName].Should().Be("ACME GmbH");
		var contact = saved.Groups[CoreSiteConfig.Contact];
		contact.Select(e => e.Key).Should().Equal("email", "address", "phone", "Fax");
		contact[0].Should().Be(new ConfigEntry("e1", "email", "Email", ConfigFieldType.Email, "hello@acme.test", null));
		contact[1].Address.Should().Be(new ConfigAddress("Main St 1", "12345", "Town", "Germany"));
		contact[2].Type.Should().Be(ConfigFieldType.Phone, "a preset's type wins over the one sent");
		contact[3].Should().Match<ConfigEntry>(e => e.Label == "Fax" && e.Type == ConfigFieldType.Phone && e.Id.Length > 0);
		saved.Logo.Should().BeEquivalentTo(new MediaRef(logo.Id, logo.Url, "Our logo"));
		saved.Icon!.Id.Should().Be(icon.Id);
		saved.UpdatedAt.Should().NotBeNull();

		var pub = await _client.GetFromJsonAsync<SiteConfigResponse>(PublicBase, Ct);
		pub.Should().BeEquivalentTo(saved);
	}

	[Fact]
	public async Task Put_ReplacesPreviousState()
	{
		var token = await CreateStaffTokenAsync();
		var logo = await UploadAsync(token);
		(await SendAsync(HttpMethod.Put, Base, token, Body(new(), Contact(Entry("phone", "1"), Entry("email", "a@b.test")), logo.Id)))
			.StatusCode.Should().Be(HttpStatusCode.OK);

		var response = await SendAsync(HttpMethod.Put, Base, token, Body(new(), Contact(Entry("email", "c@d.test"))));
		var saved = (await response.Content.ReadFromJsonAsync<SiteConfigResponse>(Ct))!;
		saved.Groups[CoreSiteConfig.Contact].Should().ContainSingle().Which.Value.Should().Be("c@d.test");
		saved.Fields[CoreSiteConfig.FirmName].Should().BeEmpty();
		saved.Logo.Should().BeNull();
	}

	public static TheoryData<string, string> InvalidBodies => new()
	{
		{ """{ "fields": { "slogan": "x" } }""", "Unknown field" },
		{ """{ "groups": { "socials": [] } }""", "Unknown group" },
		{ """{ "fields": { "firmName": "%LONG%" } }""", "exceeds" },
		{ """{ "groups": { "contact": [{ "id": "", "key": "", "type": "text", "value": "x" }] } }""", "entry names" },
		{ """{ "groups": { "contact": [{ "id": "", "key": "%KEY51%", "type": "text", "value": "x" }] } }""", "entry names" },
		{ """{ "groups": { "contact": [{ "id": "", "key": "Fax", "type": "phone", "value": "1" }, { "id": "", "key": " fax", "type": "phone", "value": "2" }] } }""", "twice" },
		{ """{ "groups": { "contact": [{ "id": "a", "key": "Fax", "type": "phone", "value": "1" }, { "id": "a", "key": "Mobile", "type": "phone", "value": "2" }] } }""", "invalid id" },
		{ """{ "groups": { "contact": [{ "id": "", "key": "email", "type": "email", "value": "not-an-email" }] } }""", "email address" },
		{ """{ "groups": { "contact": [{ "id": "", "key": "email", "type": "email", "value": "Name <a@b.test>" }] } }""", "email address" },
		{ """{ "groups": { "contact": [{ "id": "", "key": "Site", "type": "link", "value": "javascript:alert(1)" }] } }""", "http(s)" },
		{ """{ "groups": { "contact": [{ "id": "", "key": "Site", "type": "link", "value": "/relative" }] } }""", "http(s)" },
		{ """{ "groups": { "contact": [{ "id": "", "key": "address", "type": "address", "value": "", "address": { "street": "%LONG%", "postalCode": "", "city": "", "country": "" } }] } }""", "address lines" },
	};

	[Theory]
	[MemberData(nameof(InvalidBodies))]
	public async Task Put_Invalid_ReturnsBadRequestNamingTheProblem(string json, string detail)
	{
		var token = await CreateStaffTokenAsync();
		var body = JsonNode.Parse(json.Replace("%LONG%", new string('v', 1001)).Replace("%KEY51%", new string('k', 51)));
		var response = await SendAsync(HttpMethod.Put, Base, token, body);
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		(await response.Content.ReadAsStringAsync(Ct)).Should().Contain(detail);
	}

	[Fact]
	public async Task Put_UnknownType_ReturnsBadRequest()
	{
		var token = await CreateStaffTokenAsync();
		var response = await SendAsync(HttpMethod.Put, Base, token, Body(new(), Contact(Entry("Color", "red", "color"))));
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Put_TooManyEntries_ReturnsBadRequest()
	{
		var token = await CreateStaffTokenAsync();
		var entries = Enumerable.Range(0, ConfigLimits.MaxEntries + 1).Select(i => Entry($"F{i}", "x")).ToArray();
		var response = await SendAsync(HttpMethod.Put, Base, token, Body(new(), Contact(entries)));
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task ShareImage_RoundTrips_AndClearsWhenTheMediaIsDeleted()
	{
		var token = await CreateStaffTokenAsync();
		var image = await UploadAsync(token, "Team photo");
		var response = await SendAsync(HttpMethod.Put, Base, token, Body(new(), Contact(), shareImageId: image.Id));
		var saved = (await response.Content.ReadFromJsonAsync<SiteConfigResponse>(Ct))!;
		saved.ShareImage.Should().BeEquivalentTo(new MediaRef(image.Id, image.Url, "Team photo"));

		(await SendAsync(HttpMethod.Delete, $"/api/media/{image.Id}", token)).StatusCode.Should().Be(HttpStatusCode.NoContent);
		(await _client.GetFromJsonAsync<SiteConfigResponse>(PublicBase, Ct))!.ShareImage.Should().BeNull();
	}

	[Fact]
	public async Task Put_UnknownMedia_ReturnsBadRequest()
	{
		var token = await CreateStaffTokenAsync();
		var response = await SendAsync(HttpMethod.Put, Base, token, Body(new(), Contact(), iconId: Guid.NewGuid()));
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task DeletingLogoMedia_ClearsLogo()
	{
		var token = await CreateStaffTokenAsync();
		var logo = await UploadAsync(token);
		(await SendAsync(HttpMethod.Put, Base, token, Body(new(), Contact(Entry("phone", "1")), logo.Id)))
			.StatusCode.Should().Be(HttpStatusCode.OK);

		(await SendAsync(HttpMethod.Delete, $"/api/media/{logo.Id}", token))
			.StatusCode.Should().Be(HttpStatusCode.NoContent);

		var config = await _client.GetFromJsonAsync<SiteConfigResponse>(PublicBase, Ct);
		config!.Logo.Should().BeNull();
		config.Groups[CoreSiteConfig.Contact].Should().ContainSingle();
	}

	[Fact]
	public async Task InstanceSchema_AddsFieldsAndGroups_WithPresetsRequiredAndNoCustomEntries()
	{
		var cms = CmsConfigLoader.Parse("""
			{
				"siteConfig": {
					"fields": [{ "key": "vatId", "label": "VAT ID", "required": true }],
					"groups": [{
						"key": "socials", "label": "Social media", "allowCustom": false,
						"presets": [
							{ "key": "instagram", "label": "Instagram", "type": "link" },
							{ "key": "linkedin", "label": "LinkedIn", "type": "link" }
						],
						"defaults": ["instagram"], "required": ["instagram"]
					}]
				}
			}
			""");
		await using var app = _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
		{
			s.RemoveAll<CmsConfig>();
			s.AddSingleton(cms with { PublicAuth = true });
		}));
		using var client = app.CreateCookielessClient();
		var token = await CreateStaffTokenAsync();
		using (var scope = app.Services.CreateScope())
			await scope.ServiceProvider.GetRequiredService<AppDbContext>().SiteConfig.ExecuteDeleteAsync(Ct);

		var config = await client.GetFromJsonAsync<SiteConfigResponse>(PublicBase, Ct);
		config!.Fields.Keys.Should().Equal(CoreSiteConfig.FirmName, "vatId");
		config.Groups.Keys.Should().Equal(CoreSiteConfig.Contact, "socials");
		config.Groups["socials"].Select(e => e.Key).Should().Equal("instagram");

		var instance = await client.GetFromJsonAsync<InstanceConfig>("/api/public/instance", Ct);
		instance!.SiteConfig.Groups.Select(g => g.Key).Should().Equal(CoreSiteConfig.Contact, "socials");

		async Task<HttpResponseMessage> Put(string vatId, params JsonObject[] socials) =>
			await Send(client, token, Body(new JsonObject { ["vatId"] = vatId }, new JsonObject
			{
				["socials"] = new JsonArray(socials.Cast<JsonNode>().ToArray()),
			}));

		(await Put("", Entry("instagram", "https://instagram.com/acme"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
		(await Put("DE123", Entry("linkedin", "https://linkedin.com/acme"))).StatusCode
			.Should().Be(HttpStatusCode.BadRequest, "instagram is required");
		(await Put("DE123", Entry("instagram", "https://instagram.com/acme"), Entry("Mastodon", "https://x.test", "link")))
			.StatusCode.Should().Be(HttpStatusCode.BadRequest, "socials allows no custom entries");

		var ok = await Put("DE123", Entry("linkedin", "https://linkedin.com/acme"), Entry("instagram", "https://instagram.com/acme"));
		ok.StatusCode.Should().Be(HttpStatusCode.OK);
		var saved = (await ok.Content.ReadFromJsonAsync<SiteConfigResponse>(Ct))!;
		saved.Fields["vatId"].Should().Be("DE123");
		saved.Groups["socials"].Select(e => (e.Label, e.Type)).Should().Equal(
			("LinkedIn", ConfigFieldType.Link), ("Instagram", ConfigFieldType.Link));
		saved.Groups[CoreSiteConfig.Contact].Should().BeEmpty("the contact group was sent without entries");
	}

	private static JsonObject Entry(string key, string value, string type = "text", string id = "") =>
		new() { ["id"] = id, ["key"] = key, ["type"] = type, ["value"] = value };

	private static JsonObject Address(string street, string postalCode, string city, string country) => new()
	{
		["id"] = "",
		["key"] = "address",
		["type"] = "address",
		["value"] = "",
		["address"] = new JsonObject
		{
			["street"] = street,
			["postalCode"] = postalCode,
			["city"] = city,
			["country"] = country,
		},
	};

	private static JsonObject Contact(params JsonObject[] entries) =>
		new() { [CoreSiteConfig.Contact] = new JsonArray(entries.Cast<JsonNode>().ToArray()) };

	private static JsonObject Body(
		JsonObject fields, JsonObject groups, Guid? logoId = null, Guid? iconId = null, Guid? shareImageId = null) => new()
	{
		["fields"] = fields,
		["groups"] = groups,
		["logoMediaId"] = logoId?.ToString(),
		["iconMediaId"] = iconId?.ToString(),
		["shareImageMediaId"] = shareImageId?.ToString(),
	};

	private async Task<HttpResponseMessage> Send(HttpClient client, string token, JsonNode body)
	{
		using var request = new HttpRequestMessage(HttpMethod.Put, Base);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
		return await client.SendAsync(request, Ct);
	}

	private async Task<MediaItem> UploadAsync(string token, string? alt = null)
	{
		using var form = new MultipartFormDataContent();
		form.Add(new ByteArrayContent(Png), "file", "logo.png");
		if (alt is not null)
			form.Add(new StringContent(alt), "alt");
		using var request = new HttpRequestMessage(HttpMethod.Post, "/api/media") { Content = form };
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		var response = await _client.SendAsync(request, Ct);
		response.StatusCode.Should().Be(HttpStatusCode.Created);
		return (await response.Content.ReadFromJsonAsync<MediaItem>(Ct))!;
	}

	private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string token, JsonNode? body = null)
	{
		using var request = new HttpRequestMessage(method, url);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		if (body is not null)
			request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
		return await _client.SendAsync(request, Ct);
	}

	private string CreateToken(Guid id, string userType, string[] roles)
	{
		var tokens = _factory.Services.GetRequiredService<JwtTokenService>();
		return tokens.CreateAccessToken(id, $"{userType}-{id}@test.com", userType, roles).Token;
	}

	/// <summary>Config and media rows reference staff, so the row must exist.</summary>
	private async Task<string> CreateStaffTokenAsync()
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		var hasher = scope.ServiceProvider.GetRequiredService<PasswordHashService>();
		var staff = new Staff { Email = $"config-{Guid.NewGuid()}@test.com", PasswordHash = hasher.Hash("password123") };
		db.Staff.Add(staff);
		await db.SaveChangesAsync(Ct);
		return CreateToken(staff.Id, "staff", ["staff"]);
	}
}
