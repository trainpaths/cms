using api_backend.Services.Auth.JWT;
using AwesomeAssertions;

namespace api_backend.Tests.Unit.Services;

public class PasswordHashServiceTests
{
	private readonly PasswordHashService _sut = new();

	[Fact]
	public void Hash_ProducesNonEmptyHash()
	{
		var password = "test-password-123";

		var hash = _sut.Hash(password);

		hash.Should().NotBeNullOrWhiteSpace();
	}

	[Fact]
	public void Hash_DifferentPasswordsProduceDifferentHashes()
	{
		var hash1 = _sut.Hash("password-1");
		var hash2 = _sut.Hash("password-2");

		hash1.Should().NotBe(hash2);
	}

	[Fact]
	public void Hash_SamePasswordProducesDifferentHashes()
	{
		var password = "same-password";

		var hash1 = _sut.Hash(password);
		var hash2 = _sut.Hash(password);

		hash1.Should().NotBe(hash2);
	}

	[Fact]
	public void Verify_ReturnsTrueForCorrectPassword()
	{
		var password = "correct-password";
		var hash = _sut.Hash(password);

		var result = _sut.Verify(hash, password);

		result.Should().BeTrue();
	}

	[Fact]
	public void Verify_ReturnsFalseForWrongPassword()
	{
		var password = "correct-password";
		var hash = _sut.Hash(password);

		var result = _sut.Verify(hash, "wrong-password");

		result.Should().BeFalse();
	}

	[Fact]
	public void Verify_ReturnsFalseForEmptyPassword()
	{
		var hash = _sut.Hash("some-password");

		var result = _sut.Verify(hash, "");

		result.Should().BeFalse();
	}

	[Fact]
	public void Verify_IsCaseSensitive()
	{
		var password = "CaseSensitive";
		var hash = _sut.Hash(password);

		_sut.Verify(hash, "casesensitive").Should().BeFalse();
		_sut.Verify(hash, "CASESENSITIVE").Should().BeFalse();
		_sut.Verify(hash, "CaseSensitive").Should().BeTrue();
	}
}
