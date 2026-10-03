namespace api_backend.Services.Rendering;

public class RendererOptions
{
	public const string SectionName = "Renderer";

	/// <summary>Internal URL of the frontend render service. Empty → no pre-rendering (public pages fall back to the client-rendered shell).</summary>
	public string BaseUrl { get; set; } = "";

	/// <summary>Public site origin for canonical / Open Graph URLs, e.g. <c>https://example.com</c>.</summary>
	public string PublicBaseUrl { get; set; } = "http://localhost:5173";

	/// <summary>Wait after a change before rendering, so an autosave burst renders once.</summary>
	public int DebounceMilliseconds { get; set; } = 1000;

	/// <summary>How often to compare stored HTML with the renderer's version (catches deploys, missed renders).</summary>
	public int CheckIntervalSeconds { get; set; } = 60;
}
