using System.Net;
using System.Net.Http.Json;
using api_backend.Models.Dto;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace api_backend.Tests.Integration;

public class CustomerAuthTests : IClassFixture<ApiFactory>
{
	private const string Base = "/api/auth/customer";
	private readonly ApiFactory _factory;
	private readonly HttpClient _client;
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public CustomerAuthTests(ApiFactory factory)
	{
		_factory = factory;
		_client = factory.CreateCookielessClient();
	}

	private async Task<(AuthResponse Auth, string RefreshToken)> RegisterAsync(string prefix, string password = "password123")
	{
		var response = await _client.PostAsJsonAsync($"{Base}/register",
			new RegisterRequest($"{prefix}-{Guid.NewGuid()}@test.com", password, "Test"), Ct);
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Ct);
		return (auth!, AuthCookies.RefreshTokenFrom(response)!);
	}

	[Fact]
	public async Task Register_Success_ReturnsAccessTokenAndSetsRefreshCookie()
	{
		var request = new RegisterRequest($"customer-{Guid.NewGuid()}@test.com", "password123", "Test Customer");

		var response = await _client.PostAsJsonAsync($"{Base}/register", request, Ct);

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Ct);
		auth!.AccessToken.Should().NotBeNullOrWhiteSpace();
		auth.User.Email.Should().Be(request.Email.ToLowerInvariant());
		auth.User.UserType.Should().Be("customer");

		var cookie = AuthCookies.SetCookieHeader(response);
		cookie.Should().NotBeNull();
		cookie!.ToLowerInvariant().Should().Contain("httponly").And.Contain("samesite=strict")
			.And.Contain("path=/api/auth/customer");
		(await response.Content.ReadAsStringAsync(Ct)).Should().NotContain("refreshToken");
	}

	[Fact]
	public async Task Register_DuplicateEmail_ReturnsConflict()
	{
		var request = new RegisterRequest($"duplicate-{Guid.NewGuid()}@test.com", "password123", "Test");

		await _client.PostAsJsonAsync($"{Base}/register", request, Ct);
		var response = await _client.PostAsJsonAsync($"{Base}/register", request, Ct);

		response.StatusCode.Should().Be(HttpStatusCode.Conflict);
	}

	[Theory]
	[InlineData("not-an-email", "password123")]
	[InlineData("short@test.com", "short")]
	public async Task Register_InvalidInput_ReturnsBadRequest(string email, string password)
	{
		var response = await _client.PostAsJsonAsync($"{Base}/register",
			new RegisterRequest(email, password, "Test"), Ct);

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Register_OverlongFields_ReturnsBadRequest()
	{
		var tooLongPassword = new RegisterRequest($"long-{Guid.NewGuid()}@test.com", new string('a', 129), "Test");
		var tooLongName = new RegisterRequest($"long-{Guid.NewGuid()}@test.com", "password123", new string('a', 129));

		(await _client.PostAsJsonAsync($"{Base}/register", tooLongPassword, Ct))
			.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		(await _client.PostAsJsonAsync($"{Base}/register", tooLongName, Ct))
			.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Login_ValidCredentials_ReturnsTokens()
	{
		var (auth, _) = await RegisterAsync("login");

		var response = await _client.PostAsJsonAsync($"{Base}/login",
			new LoginRequest(auth.User.Email, "password123"), Ct);

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		(await response.Content.ReadFromJsonAsync<AuthResponse>(Ct))!.AccessToken.Should().NotBeNullOrWhiteSpace();
		AuthCookies.RefreshTokenFrom(response).Should().NotBeNullOrEmpty();
	}

	[Fact]
	public async Task Login_WrongPassword_ReturnsUnauthorized()
	{
		var (auth, _) = await RegisterAsync("wrongpw");

		var response = await _client.PostAsJsonAsync($"{Base}/login",
			new LoginRequest(auth.User.Email, "wrong-password"), Ct);

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Login_NonExistentUser_ReturnsUnauthorized()
	{
		var response = await _client.PostAsJsonAsync($"{Base}/login",
			new LoginRequest("nonexistent@test.com", "password123"), Ct);

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Refresh_ValidCookie_RotatesToken()
	{
		var (_, refreshToken) = await RegisterAsync("refresh");

		var response = await _client.PostWithRefreshCookieAsync($"{Base}/refresh", refreshToken, Ct);

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		(await response.Content.ReadFromJsonAsync<AuthResponse>(Ct))!.AccessToken.Should().NotBeNullOrWhiteSpace();
		AuthCookies.RefreshTokenFrom(response).Should().NotBeNullOrEmpty().And.NotBe(refreshToken);
	}

	[Fact]
	public async Task Refresh_WithoutCookie_ReturnsUnauthorized()
	{
		var response = await _client.PostWithRefreshCookieAsync($"{Base}/refresh", null, Ct);

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Refresh_InvalidToken_ReturnsUnauthorized()
	{
		var response = await _client.PostWithRefreshCookieAsync($"{Base}/refresh", "invalid-token", Ct);

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Refresh_RotatedTokenWithinGracePeriod_ReturnsUnauthorizedButKeepsSession()
	{
		var (_, original) = await RegisterAsync("grace");
		var first = await _client.PostWithRefreshCookieAsync($"{Base}/refresh", original, Ct);
		var rotated = AuthCookies.RefreshTokenFrom(first)!;

		var replay = await _client.PostWithRefreshCookieAsync($"{Base}/refresh", original, Ct);
		replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

		(await _client.PostWithRefreshCookieAsync($"{Base}/refresh", rotated, Ct))
			.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task Refresh_ReusedTokenAfterGracePeriod_RevokesAllSessions()
	{
		var (auth, original) = await RegisterAsync("reuse");
		var rotated = AuthCookies.RefreshTokenFrom(
			await _client.PostWithRefreshCookieAsync($"{Base}/refresh", original, Ct))!;

		// Pretend the rotation happened long ago, so replaying the old token counts as theft.
		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
			await db.RefreshTokens
				.Where(t => t.CustomerId == auth.User.Id && t.ReplacedByTokenId != null)
				.ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTimeOffset.UtcNow.AddMinutes(-5)), Ct);
		}

		(await _client.PostWithRefreshCookieAsync($"{Base}/refresh", original, Ct))
			.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
		(await _client.PostWithRefreshCookieAsync($"{Base}/refresh", rotated, Ct))
			.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Refresh_ConcurrentRequests_OnlyOneSucceeds()
	{
		var (_, refreshToken) = await RegisterAsync("concurrent");

		var responses = await Task.WhenAll(Enumerable.Range(0, 5)
			.Select(_ => _client.PostWithRefreshCookieAsync($"{Base}/refresh", refreshToken, Ct)));

		responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
	}

	[Fact]
	public async Task Logout_RevokesTokenAndClearsCookie()
	{
		var (_, refreshToken) = await RegisterAsync("logout");

		var response = await _client.PostWithRefreshCookieAsync($"{Base}/logout", refreshToken, Ct);

		response.StatusCode.Should().Be(HttpStatusCode.NoContent);
		AuthCookies.RefreshTokenFrom(response).Should().BeEmpty();
		(await _client.PostWithRefreshCookieAsync($"{Base}/refresh", refreshToken, Ct))
			.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task ChangePassword_RevokesSessions()
	{
		var (auth, refreshToken) = await RegisterAsync("changepw", "old-password1");

		using var request = new HttpRequestMessage(HttpMethod.Post, $"{Base}/change-password");
		request.Headers.Authorization = new("Bearer", auth.AccessToken);
		request.Content = JsonContent.Create(new ChangePasswordRequest("old-password1", "new-password1"));
		var response = await _client.SendAsync(request, Ct);

		response.StatusCode.Should().Be(HttpStatusCode.NoContent);
		(await _client.PostWithRefreshCookieAsync($"{Base}/refresh", refreshToken, Ct))
			.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
		(await _client.PostAsJsonAsync($"{Base}/login", new LoginRequest(auth.User.Email, "new-password1"), Ct))
			.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task Me_Authenticated_ReturnsUserInfo()
	{
		var (auth, _) = await RegisterAsync("me");

		using var request = new HttpRequestMessage(HttpMethod.Get, $"{Base}/me");
		request.Headers.Authorization = new("Bearer", auth.AccessToken);
		var response = await _client.SendAsync(request, Ct);

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		response.Headers.CacheControl!.NoStore.Should().BeTrue();
	}

	[Fact]
	public async Task Me_Unauthenticated_ReturnsUnauthorized()
	{
		var response = await _client.GetAsync($"{Base}/me", Ct);

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}
}
