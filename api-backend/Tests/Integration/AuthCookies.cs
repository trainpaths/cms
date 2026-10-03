using Microsoft.AspNetCore.Mvc.Testing;

namespace api_backend.Tests.Integration;

// Clients run without a cookie jar so tests can replay old refresh tokens explicitly.
internal static class AuthCookies
{
	public const string Name = "refresh_token";

	public static HttpClient CreateCookielessClient(this WebApplicationFactory<Program> factory) =>
		factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

	public static string? SetCookieHeader(HttpResponseMessage response) =>
		response.Headers.TryGetValues("Set-Cookie", out var values)
			? values.FirstOrDefault(v => v.StartsWith(Name + "=", StringComparison.Ordinal))
			: null;

	public static string? RefreshTokenFrom(HttpResponseMessage response) =>
		SetCookieHeader(response)?.Split(';')[0][(Name.Length + 1)..];

	public static async Task<HttpResponseMessage> PostWithRefreshCookieAsync(
		this HttpClient client, string url, string? refreshToken, CancellationToken ct)
	{
		using var request = new HttpRequestMessage(HttpMethod.Post, url);
		if (refreshToken is not null)
			request.Headers.Add("Cookie", $"{Name}={refreshToken}");
		return await client.SendAsync(request, ct);
	}
}
