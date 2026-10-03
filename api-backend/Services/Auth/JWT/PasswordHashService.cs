using Microsoft.AspNetCore.Identity;

namespace api_backend.Services.Auth.JWT;

/// <summary>
/// Thin wrapper over ASP.NET Core's <see cref="PasswordHasher{TUser}"/> (PBKDF2).
/// Generic placeholder type since we hash plain strings, not a specific user entity.
/// </summary>
public class PasswordHashService
{
	private readonly PasswordHasher<object> _hasher = new();
	private static readonly object Placeholder = new();

	// Used for unknown emails so login timing doesn't reveal which accounts exist.
	private readonly Lazy<string> _dummyHash;

	public PasswordHashService()
	{
		_dummyHash = new Lazy<string>(() => _hasher.HashPassword(Placeholder, Guid.NewGuid().ToString()));
	}

	public string Hash(string password) => _hasher.HashPassword(Placeholder, password);

	public bool Verify(string hash, string password) => Verify(hash, password, out _);

	public bool Verify(string hash, string password, out bool needsRehash)
	{
		var result = _hasher.VerifyHashedPassword(Placeholder, hash, password);
		needsRehash = result is PasswordVerificationResult.SuccessRehashNeeded;
		return result is PasswordVerificationResult.Success
			or PasswordVerificationResult.SuccessRehashNeeded;
	}

	public void VerifyDummy(string password) =>
		_hasher.VerifyHashedPassword(Placeholder, _dummyHash.Value, password);
}
