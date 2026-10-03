using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using api_backend.Models.Auth;
using api_backend.Models.Dto;
using api_backend.Services.Auth.JWT;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace api_backend.Tests.Integration;

public class StaffAuthTests : IClassFixture<ApiFactory>
{
	private const string Base = "/api/auth/staff";
	private readonly HttpClient _client;
	private readonly ApiFactory _factory;
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public StaffAuthTests(ApiFactory factory)
	{
		_factory = factory;
		_client = factory.CreateCookielessClient();
	}

	[Fact]
	public async Task Register_WithoutAuth_ReturnsUnauthorized()
	{
		var response = await _client.PostAsJsonAsync($"{Base}/register",
			new StaffRegisterRequest($"staff-{Guid.NewGuid()}@test.com", "password123", "Test Staff", null, ["staff"]), Ct);

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Register_WithSuperAdmin_ReturnsUserInfoWithoutSession()
	{
		var superAdminToken = await CreateSuperAdminAndGetToken();
		var request = new StaffRegisterRequest(
			$"newstaff-{Guid.NewGuid()}@test.com", "password123", "New Staff", "org-123", ["staff"]);

		var response = await RegisterStaffAsync(superAdminToken, request);

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var user = await response.Content.ReadFromJsonAsync<UserInfo>(Ct);
		user!.Email.Should().Be(request.Email.ToLowerInvariant());
		user.UserType.Should().Be("staff");
		user.OrganizationId.Should().Be("org-123");
		user.Roles.Should().Contain("staff");
		AuthCookies.SetCookieHeader(response).Should().BeNull();
	}

	[Fact]
	public async Task Register_WithRegularStaff_ReturnsForbidden()
	{
		var superAdminToken = await CreateSuperAdminAndGetToken();
		var (staffAuth, _) = await CreateStaffAndLoginAsync(superAdminToken, "regularstaff", ["staff"]);

		var response = await RegisterStaffAsync(staffAuth.AccessToken,
			new StaffRegisterRequest($"another-{Guid.NewGuid()}@test.com", "password123", "Another", null, null));

		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Register_DuplicateEmail_ReturnsConflict()
	{
		var superAdminToken = await CreateSuperAdminAndGetToken();
		var request = new StaffRegisterRequest($"dupstaff-{Guid.NewGuid()}@test.com", "password123", "First", null, null);

		await RegisterStaffAsync(superAdminToken, request);
		var response = await RegisterStaffAsync(superAdminToken, request);

		response.StatusCode.Should().Be(HttpStatusCode.Conflict);
	}

	[Fact]
	public async Task Login_ValidCredentials_ReturnsTokens()
	{
		var superAdminToken = await CreateSuperAdminAndGetToken();

		var (auth, refreshToken) = await CreateStaffAndLoginAsync(superAdminToken, "loginstaff", ["staff"]);

		auth.AccessToken.Should().NotBeNullOrWhiteSpace();
		auth.User.Roles.Should().Contain("staff");
		refreshToken.Should().NotBeNullOrEmpty();
	}

	[Fact]
	public async Task Login_WrongPassword_ReturnsUnauthorized()
	{
		var superAdminToken = await CreateSuperAdminAndGetToken();
		var email = $"wrongpwstaff-{Guid.NewGuid()}@test.com";
		await RegisterStaffAsync(superAdminToken, new StaffRegisterRequest(email, "password123", "Test", null, null));

		var response = await _client.PostAsJsonAsync($"{Base}/login", new LoginRequest(email, "wrong-password"), Ct);

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Refresh_ValidCookie_RotatesToken()
	{
		var superAdminToken = await CreateSuperAdminAndGetToken();
		var (_, refreshToken) = await CreateStaffAndLoginAsync(superAdminToken, "refreshstaff", null);

		var response = await _client.PostWithRefreshCookieAsync($"{Base}/refresh", refreshToken, Ct);

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		AuthCookies.RefreshTokenFrom(response).Should().NotBeNullOrEmpty().And.NotBe(refreshToken);
	}

	[Fact]
	public async Task Refresh_CustomerToken_ReturnsUnauthorized()
	{
		var register = await _client.PostAsJsonAsync("/api/auth/customer/register",
			new RegisterRequest($"cross-{Guid.NewGuid()}@test.com", "password123", "Customer"), Ct);

		var response = await _client.PostWithRefreshCookieAsync(
			$"{Base}/refresh", AuthCookies.RefreshTokenFrom(register), Ct);

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Me_WithStaffToken_ReturnsUserInfo()
	{
		var superAdminToken = await CreateSuperAdminAndGetToken();
		var (auth, _) = await CreateStaffAndLoginAsync(superAdminToken, "mestaff", ["staff"], "org-xyz");

		using var meRequest = new HttpRequestMessage(HttpMethod.Get, $"{Base}/me");
		meRequest.Headers.Authorization = new("Bearer", auth.AccessToken);
		var response = await _client.SendAsync(meRequest, Ct);

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
		body.GetProperty("email").GetString().Should().Be(auth.User.Email);
		body.GetProperty("userType").GetString().Should().Be("staff");
		body.GetProperty("organizationId").GetString().Should().Be("org-xyz");
		body.GetProperty("roles").EnumerateArray().Should().Contain(r => r.GetString() == "staff");
	}

	[Fact]
	public async Task Me_WithoutAuth_ReturnsUnauthorized()
	{
		var response = await _client.GetAsync($"{Base}/me", Ct);

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Me_WithCustomerToken_ReturnsForbidden()
	{
		var registerResponse = await _client.PostAsJsonAsync("/api/auth/customer/register",
			new RegisterRequest($"customer-{Guid.NewGuid()}@test.com", "password123", "Customer"), Ct);
		var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(Ct);

		using var meRequest = new HttpRequestMessage(HttpMethod.Get, $"{Base}/me");
		meRequest.Headers.Authorization = new("Bearer", auth!.AccessToken);
		var response = await _client.SendAsync(meRequest, Ct);

		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Logout_RevokesRefreshToken()
	{
		var superAdminToken = await CreateSuperAdminAndGetToken();
		var (_, refreshToken) = await CreateStaffAndLoginAsync(superAdminToken, "logoutstaff", null);

		var logoutResponse = await _client.PostWithRefreshCookieAsync($"{Base}/logout", refreshToken, Ct);
		logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

		var refreshResponse = await _client.PostWithRefreshCookieAsync($"{Base}/refresh", refreshToken, Ct);
		refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Logout_InvalidToken_ReturnsNoContent()
	{
		var response = await _client.PostWithRefreshCookieAsync($"{Base}/logout", "invalid-token", Ct);

		response.StatusCode.Should().Be(HttpStatusCode.NoContent);
	}

	private async Task<HttpResponseMessage> RegisterStaffAsync(string bearerToken, StaffRegisterRequest request)
	{
		using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{Base}/register");
		httpRequest.Headers.Authorization = new("Bearer", bearerToken);
		httpRequest.Content = JsonContent.Create(request);
		return await _client.SendAsync(httpRequest, Ct);
	}

	private async Task<(AuthResponse Auth, string RefreshToken)> CreateStaffAndLoginAsync(
		string superAdminToken, string prefix, string[]? roles, string? organizationId = null)
	{
		var email = $"{prefix}-{Guid.NewGuid()}@test.com";
		var register = await RegisterStaffAsync(superAdminToken,
			new StaffRegisterRequest(email, "password123", "Test", organizationId, roles));
		register.StatusCode.Should().Be(HttpStatusCode.OK);

		var login = await _client.PostAsJsonAsync($"{Base}/login", new LoginRequest(email, "password123"), Ct);
		login.StatusCode.Should().Be(HttpStatusCode.OK);
		var auth = await login.Content.ReadFromJsonAsync<AuthResponse>(Ct);
		return (auth!, AuthCookies.RefreshTokenFrom(login)!);
	}

	private async Task<string> CreateSuperAdminAndGetToken()
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		var hasher = scope.ServiceProvider.GetRequiredService<PasswordHashService>();
		var tokens = scope.ServiceProvider.GetRequiredService<JwtTokenService>();

		var superAdminRole = await db.Roles.FirstAsync(r => r.Name == "super_admin", Ct);
		var staff = new Staff
		{
			Email = $"superadmin-{Guid.NewGuid()}@test.com",
			PasswordHash = hasher.Hash("password123"),
			DisplayName = "Super Admin",
		};
		staff.StaffRoles.Add(new StaffRole { Staff = staff, Role = superAdminRole });
		db.Staff.Add(staff);
		await db.SaveChangesAsync(Ct);

		return tokens.CreateAccessToken(staff.Id, staff.Email, "staff", ["super_admin"]).Token;
	}
}
