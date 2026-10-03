using api_backend.Models.Dto;
using api_backend.Services.Auth;
using AwesomeAssertions;

namespace api_backend.Tests.Unit.Services;

public class StaffAuthServiceTests : AuthServiceTestBase
{
	private StaffAuthService CreateSut(AppDbContext db) => new(db, Hasher, Tokens, RefreshTokens(db));

	private static StaffRegisterRequest Request(string email, string[]? roles = null, string? org = null) =>
		new(email, "password123", "Test", org, roles);

	[Fact]
	public async Task RegisterAsync_NormalizesEmail()
	{
		using var db = TestDbContextFactory.Create();

		var result = await CreateSut(db).RegisterAsync(Request("  TEST@EXAMPLE.COM  "), Ct);

		result!.Email.Should().Be("test@example.com");
	}

	[Fact]
	public async Task RegisterAsync_DuplicateEmail_ReturnsNull()
	{
		using var db = TestDbContextFactory.Create();
		var sut = CreateSut(db);

		await sut.RegisterAsync(Request("dupe@test.com"), Ct);
		var result = await sut.RegisterAsync(Request("dupe@test.com"), Ct);

		result.Should().BeNull();
	}

	[Fact]
	public async Task RegisterAsync_DoesNotIssueTokens()
	{
		using var db = TestDbContextFactory.Create();

		await CreateSut(db).RegisterAsync(Request("notokens@test.com"), Ct);

		db.RefreshTokens.Should().BeEmpty();
	}

	[Fact]
	public async Task RegisterAsync_AssignsRequestedRoles()
	{
		using var db = TestDbContextFactory.Create();

		var result = await CreateSut(db).RegisterAsync(Request("roles@test.com", ["super_admin", "staff"]), Ct);

		result!.Roles.Should().BeEquivalentTo("super_admin", "staff");
	}

	[Fact]
	public async Task RegisterAsync_DefaultsToStaffRole()
	{
		using var db = TestDbContextFactory.Create();

		var result = await CreateSut(db).RegisterAsync(Request("default@test.com"), Ct);

		result!.Roles.Should().BeEquivalentTo("staff");
	}

	[Fact]
	public async Task RegisterAsync_IgnoresNonExistentRoles()
	{
		using var db = TestDbContextFactory.Create();

		var result = await CreateSut(db).RegisterAsync(Request("ignore@test.com", ["staff", "does_not_exist"]), Ct);

		result!.Roles.Should().BeEquivalentTo("staff");
	}

	[Fact]
	public async Task RegisterAsync_SetsOrganizationId()
	{
		using var db = TestDbContextFactory.Create();

		var result = await CreateSut(db).RegisterAsync(Request("org@test.com", org: "org-123"), Ct);

		result!.OrganizationId.Should().Be("org-123");
	}

	[Fact]
	public async Task LoginAsync_ValidCredentials_ReturnsTokensWithRoles()
	{
		using var db = TestDbContextFactory.Create();
		var sut = CreateSut(db);
		await sut.RegisterAsync(Request("login@test.com", ["staff"]), Ct);

		var result = await sut.LoginAsync(new LoginRequest("login@test.com", "password123"), "127.0.0.1", Ct);

		result!.AccessToken.Should().NotBeNullOrWhiteSpace();
		result.Refresh.RawToken.Should().NotBeNullOrWhiteSpace();
		result.User.Roles.Should().Contain("staff");
	}

	[Fact]
	public async Task LoginAsync_WrongPassword_ReturnsNull()
	{
		using var db = TestDbContextFactory.Create();
		var sut = CreateSut(db);
		await sut.RegisterAsync(Request("wrongpw@test.com"), Ct);

		var result = await sut.LoginAsync(new LoginRequest("wrongpw@test.com", "wrongpassword"), "127.0.0.1", Ct);

		result.Should().BeNull();
	}

	[Fact]
	public async Task LoginAsync_NonExistentUser_ReturnsNull()
	{
		using var db = TestDbContextFactory.Create();

		var result = await CreateSut(db).LoginAsync(new LoginRequest("nouser@test.com", "password123"), "127.0.0.1", Ct);

		result.Should().BeNull();
	}

	[Fact]
	public async Task LoginAsync_InactiveStaff_ReturnsNull()
	{
		using var db = TestDbContextFactory.Create();
		var sut = CreateSut(db);
		await sut.RegisterAsync(Request("inactive@test.com"), Ct);
		db.Staff.First(s => s.Email == "inactive@test.com").IsActive = false;
		await db.SaveChangesAsync(Ct);

		var result = await sut.LoginAsync(new LoginRequest("inactive@test.com", "password123"), "127.0.0.1", Ct);

		result.Should().BeNull();
	}

	[Fact]
	public async Task LoginAsync_UpdatesLastLoginAt()
	{
		using var db = TestDbContextFactory.Create();
		var sut = CreateSut(db);
		await sut.RegisterAsync(Request("lastlogin@test.com"), Ct);

		var before = DateTimeOffset.UtcNow;
		await sut.LoginAsync(new LoginRequest("lastlogin@test.com", "password123"), "127.0.0.1", Ct);

		db.Staff.First(s => s.Email == "lastlogin@test.com").LastLoginAt.Should().BeOnOrAfter(before);
	}

	[Fact]
	public async Task LogoutAsync_RevokesToken()
	{
		using var db = TestDbContextFactory.Create();
		var sut = CreateSut(db);
		await sut.RegisterAsync(Request("logout@test.com"), Ct);
		var auth = await sut.LoginAsync(new LoginRequest("logout@test.com", "password123"), "127.0.0.1", Ct);

		var result = await sut.LogoutAsync(auth!.Refresh.RawToken, Ct);

		result.Should().BeTrue();
		db.RefreshTokens.Single().RevokedAt.Should().NotBeNull();
	}

	[Fact]
	public async Task LogoutAsync_AlreadyRevokedToken_ReturnsFalse()
	{
		using var db = TestDbContextFactory.Create();
		var sut = CreateSut(db);
		await sut.RegisterAsync(Request("revoked@test.com"), Ct);
		var auth = await sut.LoginAsync(new LoginRequest("revoked@test.com", "password123"), "127.0.0.1", Ct);

		await sut.LogoutAsync(auth!.Refresh.RawToken, Ct);
		var result = await sut.LogoutAsync(auth.Refresh.RawToken, Ct);

		result.Should().BeFalse();
	}
}
