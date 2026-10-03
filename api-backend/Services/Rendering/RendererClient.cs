using System.Net.Http.Json;
using System.Text.Json;
using api_backend.Models.Dto;
using Microsoft.Extensions.Options;

namespace api_backend.Services.Rendering;

/// <summary>
/// What one public page renders from; mirrors <c>PublicState</c> in <c>frontend/src/public/state.ts</c>.
/// <see cref="Page"/> null = the not-found page.
/// </summary>
public record RenderState(string Slug, PublicPage? Page, PublicMenu? Menu, SiteConfigResponse? Config, string BaseUrl);

public record RenderResult(string Html, string Version);

public interface IRendererClient
{
	bool Enabled { get; }
	Task<string> GetVersionAsync(CancellationToken ct);
	Task<RenderResult> RenderAsync(RenderState state, CancellationToken ct);
}

/// <summary>HTTP client for the frontend render service (<c>frontend/server/render-server.ts</c>).</summary>
public sealed class HttpRendererClient(HttpClient http, IOptions<RendererOptions> options) : IRendererClient
{
	// same shape as the public API responses the client hydrates from
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

	public bool Enabled => !string.IsNullOrWhiteSpace(options.Value.BaseUrl);

	public async Task<string> GetVersionAsync(CancellationToken ct) =>
		(await http.GetStringAsync("version", ct)).Trim();

	public async Task<RenderResult> RenderAsync(RenderState state, CancellationToken ct)
	{
		using var response = await http.PostAsJsonAsync("render", state, Json, ct);
		response.EnsureSuccessStatusCode();
		var version = response.Headers.TryGetValues("X-Renderer-Version", out var values) ? values.First() : "";
		return new RenderResult(await response.Content.ReadAsStringAsync(ct), version);
	}
}
