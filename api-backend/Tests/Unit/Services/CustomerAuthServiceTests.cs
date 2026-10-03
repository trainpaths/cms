using api_backend.Models.Dto;
using api_backend.Services.Auth;
using AwesomeAssertions;

namespace api_backend.Tests.Unit.Services;

public class CustomerAuthServiceTests : AuthServiceTestBase
{
	private CustomerAuthService CreateSut(AppDbContext db) => new(db, Hasher, Tokens, RefreshTokens(db));

	[Fact]
	public async Task RegisterAsync_NormalizesEmail()
	{
		using var db = TestDbContextFactory.Create();
		var sut = CreateSut(db);

		var result = await sut.RegisterAsync(new RegisterRequest("  TEST@EXAMPLE.COM  ", "password123", "Test"), "127.0.0.1", Ct);

		result.Should().NotBeNull();
		result!.User.Email.Should().Be("test@example.com");
	}

	[Fact]
	public async Task RegisterAsync_DuplicateEmail_ReturnsNull()
	{
		using var db = TestDbContextFactory.Create();
		var sut = CreateSut(db);

		await sut.RegisterAsync(new RegisterRequest("dupe@test.com", "password123", "First"), "127.0.0.1", Ct);
		var result = await sut.RegisterAsync(new RegisterRequest("dupe@test.com", "password123", "Second"), "127.0.0.1", Ct);

		result.Should().BeNull();
	}

	[Fact]
	public async Task RegisterAsync_ReturnsTokensAndPersistsRefreshToken()
	{
		using var db = TestDbContextFactory.Create();
		var sut = CreateSut(db);

		var result = await sut.RegisterAsync(new RegisterRequest("new@test.com", "password123", "Test"), "127.0.0.1", Ct);

		result!.AccessToken.Should().NotBeNullOrWhiteSpace();
		result.Refresh.RawToken.Should().NotBeNullOrWhiteSpace();
		result.User.UserType.Should().Be("customer");
		db.RefreshTokens.Single().TokenHash.Should().Be(Tokens.HashRefreshToken(result.Refresh.RawToken));
	}

	[Fact]
	public async Task LoginAsync_ValidCredentials_ReturnsTokens()
	{
		using var db = TestDbContextFactory.Create();
		var sut = CreateSut(db);

		await sut.RegisterAsync(new RegisterRequest("login@test.com", "password123", "Test"), "127.0.0.1", Ct);
		var result = await sut.LoginAsync(new LoginRequest("login@test.com", "password123"), "127.0.0.1", Ct);

		result!.AccessToken.Should().NotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task LoginAsync_WrongPassword_ReturnsNull()
	{
		using var db = TestDbContextFactory.Create();
		var sut = CreateSut(db);

		await sut.RegisterAsync(new RegisterRequest("wrongpw@test.com", "password123", "Test"), "127.0.0.1", Ct);
		var result = await sut.LoginAsync(new LoginRequest("wrongpw@test.com", "wrongpassword"), "127.0.0.1", Ct);

		result.Should().BeNull();
	}

	[Fact]
	public async Task LoginAsync_NonExistentUser_ReturnsNull()
	{
		using var db = TestDbContextFactory.Create();
		var sut = CreateSut(db);

		var result = await sut.LoginAsync(new LoginRequest("nouser@test.com", "password123"), "127.0.0.1", Ct);

		result.Should().BeNull();
	}

	[Fact]
	public async Task LoginAsync_InactiveCustomer_ReturnsNull()
	{
		using var db = TestDbContextFactory.Create();
		var sut = CreateSut(db);

		await sut.RegisterAsync(new RegisterRequest("inactive@test.com", "password123", "Test"), "127.0.0.1", Ct);
		db.Customers.First(c => c.Email == "inactive@test.com").IsActive = false;
		await db.SaveChangesAsync(Ct);

		var result = await sut.LoginAsync(new LoginRequest("inactive@test.com", "password123"), "127.0.0.1", Ct);

		result.Should().BeNull();
	}

	[Fact]
	public async Task LoginAsync_UpdatesLastLoginAt()
	{
		using var db = TestDbContextFactory.Create();
		var sut = CreateSut(db);

		await sut.RegisterAsync(new RegisterRequest("lastlogin@test.com", "password123", "Test"), "127.0.0.1", Ct);

		var before = DateTimeOffset.UtcNow;
		await sut.LoginAsync(new LoginRequest("lastlogin@test.com", "password123"), "127.0.0.1", Ct);
		var after = DateTimeOffset.UtcNow;

		var customer = db.Customers.First(c => c.Email == "lastlogin@test.com");
		customer.LastLoginAt.Should().NotBeNull();
		customer.LastLoginAt.Should().BeOnOrAfter(before);
		customer.LastLoginAt.Should().BeOnOrBefore(after);
	}

	[Fact]
	public async Task LogoutAsync_RevokesToken()
	{
		using var db = TestDbContextFactory.Create();
		var sut = CreateSut(db);

		var auth = await sut.RegisterAsync(new RegisterRequest("logout@test.com", "password123", "Test"), "127.0.0.1", Ct);
		var result = await sut.LogoutAsync(auth!.Refresh.RawToken, Ct);

		result.Should().BeTrue();
		db.RefreshTokens.First().RevokedAt.Should().NotBeNull();
	}

	[Theory]
	[InlineData("invalid-token")]
	[InlineData(null)]
	public async Task LogoutAsync_InvalidToken_ReturnsFalse(string? token)
	{
		using var db = TestDbContextFactory.Create();
		var sut = CreateSut(db);

		var result = await sut.LogoutAsync(token, Ct);

		result.Should().BeFalse();
	}

	[Fact]
	public async Task UpdateProfileAsync_UpdatesDisplayName()
	{
		using var db = TestDbContextFactory.Create();
		var sut = CreateSut(db);

		var auth = await sut.RegisterAsync(new RegisterRequest("profile@test.com", "password123", "Old"), "127.0.0.1", Ct);
		var result = await sut.UpdateProfileAsync(auth!.User.Id, "New", Ct);

		result!.DisplayName.Should().Be("New");
		(await sut.GetMeAsync(auth.User.Id, Ct))!.DisplayName.Should().Be("New");
	}
}
