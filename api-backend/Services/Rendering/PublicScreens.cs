namespace api_backend.Services.Rendering;

/// <summary>
/// Customer screens of the public site (auth flows + account pages under <c>user/</c>): rendered by the public app like
/// pages, but kept in memory (<see cref="RenderStatus.Screens"/>) and only with <c>publicAuth</c>. Paths without the
/// leading slash = the <c>slug</c> of their <see cref="RenderState"/>. Mirrors <c>SCREENS</c> in
/// <c>frontend/src/public/screens/index.ts</c> and <c>frontend/server/screens.js</c>.
/// </summary>
public static class PublicScreens
{
	public static readonly IReadOnlyList<string> All =
	[
		"login", "register", "forgot-password", "reset-password", "verify-email", "user/profile", "user/password",
	];

	public static bool Contains(string path) => All.Contains(path);
}
