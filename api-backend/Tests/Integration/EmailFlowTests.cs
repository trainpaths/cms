using System.Net;
using System.Net.Http.Json;
using api_backend.Models.Dto;
using api_backend.Models.Auth;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace api_backend.Tests.Integration;

public class EmailFlowTests : IClassFixture<ApiFactory>
{
	private readonly ApiFactory _factory;
	private readonly HttpClient _client;
	private CancellationToken Ct => TestContext.Current.CancellationToken;

	public EmailFlowTests(ApiFactory factory)
	{
		_factory = factory;
		_client = factory.CreateCookielessClient();
	}

	private async Task<string> RegisterAsync(string email, string password = "password123")
	{
		var response = await _client.PostAsJsonAsync("/api/auth/customer/register",
			new RegisterRequest(email, password, "Test"), Ct);
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		return email;
	}

	private async Task<bool> IsEmailConfirmedAsync(string email)
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		var customer = await db.Customers.AsNoTracking().FirstAsync(c => c.Email == email, Ct);
		return customer.IsEmailConfirmed;
	}

	// ---- Email verification ----

	[Fact]
	public async Task RequestVerification_ThenConfirm_SetsEmailConfirmed()
	{
		var email = await RegisterAsync($"verify-{Guid.NewGuid()}@test.com");

		var request = await _client.PostAsJsonAsync("/api/auth/customer/request-verification",
			new RequestVerificationRequest(email), Ct);
		request.StatusCode.Should().Be(HttpStatusCode.NoContent);

		var token = await _factory.EmailSender.WaitForTokenAsync(email, ct: Ct);
		token.Should().NotBeNullOrWhiteSpace();

		var confirm = await _client.PostAsJsonAsync("/api/auth/customer/confirm-email",
			new ConfirmEmailRequest(token!), Ct);
		confirm.StatusCode.Should().Be(HttpStatusCode.NoContent);

		(await IsEmailConfirmedAsync(email)).Should().BeTrue();
	}

	[Fact]
	public async Task ConfirmEmail_ReusedToken_ReturnsBadRequest()
	{
		var email = await RegisterAsync($"verify-reuse-{Guid.NewGuid()}@test.com");
		await _client.PostAsJsonAsync("/api/auth/customer/request-verification",
			new RequestVerificationRequest(email), Ct);
		var token = (await _factory.EmailSender.WaitForTokenAsync(email, ct: Ct))!;

		var first = await _client.PostAsJsonAsync("/api/auth/customer/confirm-email",
			new ConfirmEmailRequest(token), Ct);
		first.StatusCode.Should().Be(HttpStatusCode.NoContent);

		var second = await _client.PostAsJsonAsync("/api/auth/customer/confirm-email",
			new ConfirmEmailRequest(token), Ct);
		second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task ConfirmEmail_InvalidToken_ReturnsBadRequest()
	{
		var response = await _client.PostAsJsonAsync("/api/auth/customer/confirm-email",
			new ConfirmEmailRequest("not-a-real-token"), Ct);

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	// ---- Password reset ----

	[Fact]
	public async Task ForgotPassword_IdenticalResponse_ForKnownAndUnknownEmail()
	{
		var known = await RegisterAsync($"forgot-known-{Guid.NewGuid()}@test.com");
		var unknown = $"forgot-unknown-{Guid.NewGuid()}@test.com";

		var knownResponse = await _client.PostAsJsonAsync("/api/auth/customer/forgot-password",
			new ForgotPasswordRequest(known), Ct);
		var unknownResponse = await _client.PostAsJsonAsync("/api/auth/customer/forgot-password",
			new ForgotPasswordRequest(unknown), Ct);

		knownResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
		unknownResponse.StatusCode.Should().Be(unknownResponse.StatusCode);
		unknownResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

		(await knownResponse.Content.ReadAsStringAsync(Ct))
			.Should().Be(await unknownResponse.Content.ReadAsStringAsync(Ct));
	}

	[Fact]
	public async Task ResetPassword_SetsNewPassword_RevokesSessions_AndTokenIsSingleUse()
	{
		var email = await RegisterAsync($"reset-{Guid.NewGuid()}@test.com", "old-password1");

		// An active session that must be revoked by the reset.
		var login = await _client.PostAsJsonAsync("/api/auth/customer/login",
			new LoginRequest(email, "old-password1"), Ct);
		var refreshToken = AuthCookies.RefreshTokenFrom(login);

		await _client.PostAsJsonAsync("/api/auth/customer/forgot-password",
			new ForgotPasswordRequest(email), Ct);
		var token = (await _factory.EmailSender.WaitForTokenAsync(email, ct: Ct))!;

		var reset = await _client.PostAsJsonAsync("/api/auth/customer/reset-password",
			new ResetPasswordRequest(token, "new-password1"), Ct);
		reset.StatusCode.Should().Be(HttpStatusCode.NoContent);

		var refresh = await _client.PostWithRefreshCookieAsync("/api/auth/customer/refresh", refreshToken, Ct);
		refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

		var oldLogin = await _client.PostAsJsonAsync("/api/auth/customer/login",
			new LoginRequest(email, "old-password1"), Ct);
		oldLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

		(await IsEmailConfirmedAsync(email)).Should().BeTrue();

		var newLogin = await _client.PostAsJsonAsync("/api/auth/customer/login",
			new LoginRequest(email, "new-password1"), Ct);
		newLogin.StatusCode.Should().Be(HttpStatusCode.OK);

		var reuse = await _client.PostAsJsonAsync("/api/auth/customer/reset-password",
			new ResetPasswordRequest(token, "another-password1"), Ct);
		reuse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task ForgotPassword_NewRequest_InvalidatesEarlierToken()
	{
		var email = await RegisterAsync($"forgot-twice-{Guid.NewGuid()}@test.com");

		await _client.PostAsJsonAsync("/api/auth/customer/forgot-password", new ForgotPasswordRequest(email), Ct);
		var first = (await _factory.EmailSender.WaitForTokenAsync(email, ct: Ct))!;
		await _client.PostAsJsonAsync("/api/auth/customer/forgot-password", new ForgotPasswordRequest(email), Ct);
		var second = (await _factory.EmailSender.WaitForTokenAsync(email, first, Ct))!;

		(await _client.PostAsJsonAsync("/api/auth/customer/reset-password",
			new ResetPasswordRequest(first, "new-password1"), Ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
		(await _client.PostAsJsonAsync("/api/auth/customer/reset-password",
			new ResetPasswordRequest(second, "new-password1"), Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
	}

	[Fact]
	public async Task ResetPassword_InvalidToken_ReturnsBadRequest()
	{
		var response = await _client.PostAsJsonAsync("/api/auth/customer/reset-password",
			new ResetPasswordRequest("not-a-real-token", "new-password1"), Ct);

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}
}
