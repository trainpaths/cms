using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace api_backend.Services.Auth;

public readonly record struct GeneratedSecureToken(string RawToken, string TokenHash);

/// <summary>Opaque tokens (refresh, email links): 64 random bytes as base64url; only the SHA-256 hash is stored.</summary>
public static class SecureToken
{
	public static GeneratedSecureToken Create()
	{
		var raw = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(64));
		return new GeneratedSecureToken(raw, Hash(raw));
	}

	public static string Hash(string rawToken) =>
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
