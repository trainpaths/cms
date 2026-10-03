using System.Buffers.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using api_backend.Models.Auth.JWT;
using api_backend.Services.Auth.JWT;
using AwesomeAssertions;
using Microsoft.Extensions.Options;

namespace api_backend.Tests.Unit.Services;

public class JwtTokenServiceTests
{
	private readonly JwtTokenService _sut;
	private readonly JwtOptions _options;

	public JwtTokenServiceTests()
	{
		_options = new JwtOptions
		{
			Issuer = "test-issuer",
			Audience = "test-audience",
			SigningKey = "test-signing-key-must-be-at-least-32-characters-long",
			AccessTokenMinutes = 15,
			RefreshTokenDays = 30,
		};
		_sut = new JwtTokenService(Options.Create(_options));
	}

	[Fact]
	public void CreateAccessToken_GeneratesValidJwt()
	{
		var userId = Guid.NewGuid();
		var email = "test@example.com";
		var userType = AuthClaims.CustomerUserType;
		var roles = new[] { "staff" };

		var result = _sut.CreateAccessToken(userId, email, userType, roles);

		result.Should().NotBeNull();
		result.Token.Should().NotBeNullOrWhiteSpace();
		result.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
		result.ExpiresAt.Should().BeCloseTo(
			DateTimeOffset.UtcNow.AddMinutes(_options.AccessTokenMinutes),
			TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void CreateAccessToken_ContainsCorrectClaims()
	{
		var userId = Guid.NewGuid();
		var email = "test@example.com";
		var userType = AuthClaims.StaffUserType;
		var roles = new[] { "super_admin", "staff" };
		var orgId = "org-123";

		var result = _sut.CreateAccessToken(userId, email, userType, roles, orgId);

		var jwt = new JsonWebTokenHandler().ReadJsonWebToken(result.Token);

		jwt.Issuer.Should().Be(_options.Issuer);
		jwt.Audiences.Should().Contain(_options.Audience);
		jwt.Subject.Should().Be(userId.ToString());
		jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == email);
		jwt.Claims.Should().Contain(c => c.Type == AuthClaims.UserType && c.Value == userType);
		jwt.Claims.Should().Contain(c => c.Type == AuthClaims.OrganizationId && c.Value == orgId);
		jwt.Claims.Where(c => c.Type == "role").Select(c => c.Value)
			.Should().BeEquivalentTo(roles);
	}

	[Fact]
	public void CreateAccessToken_OmitsOrganizationIdWhenNull()
	{
		var userId = Guid.NewGuid();
		var email = "test@example.com";

		var result = _sut.CreateAccessToken(userId, email, AuthClaims.CustomerUserType, []);

		var jwt = new JsonWebTokenHandler().ReadJsonWebToken(result.Token);

		jwt.Claims.Should().NotContain(c => c.Type == AuthClaims.OrganizationId);
	}

	[Fact]
	public void CreateRefreshToken_GeneratesUniqueTokens()
	{
		var token1 = _sut.CreateRefreshToken();
		var token2 = _sut.CreateRefreshToken();

		token1.RawToken.Should().NotBe(token2.RawToken);
		token1.TokenHash.Should().NotBe(token2.TokenHash);
	}

	[Fact]
	public void CreateRefreshToken_HasCorrectExpiry()
	{
		var result = _sut.CreateRefreshToken();

		result.ExpiresAt.Should().BeCloseTo(
			DateTimeOffset.UtcNow.AddDays(_options.RefreshTokenDays),
			TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void CreateRefreshToken_RawTokenIsBase64Url()
	{
		var result = _sut.CreateRefreshToken();

		result.RawToken.Should().MatchRegex("^[A-Za-z0-9_-]+$");
		Base64Url.DecodeFromChars(result.RawToken).Should().HaveCount(64);
	}

	[Fact]
	public void HashRefreshToken_ProducesConsistentHashes()
	{
		var rawToken = "test-token-value";

		var hash1 = _sut.HashRefreshToken(rawToken);
		var hash2 = _sut.HashRefreshToken(rawToken);

		hash1.Should().Be(hash2);
	}

	[Fact]
	public void HashRefreshToken_ProducesHexString()
	{
		var rawToken = "test-token-value";

		var hash = _sut.HashRefreshToken(rawToken);

		hash.Should().MatchRegex("^[0-9A-F]+$");
		hash.Should().HaveLength(64);
	}

	[Fact]
	public void HashRefreshToken_DifferentInputsProduceDifferentHashes()
	{
		var hash1 = _sut.HashRefreshToken("token-1");
		var hash2 = _sut.HashRefreshToken("token-2");

		hash1.Should().NotBe(hash2);
	}
}
