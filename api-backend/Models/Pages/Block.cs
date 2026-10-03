using System.Text.Json;

namespace api_backend.Models.Pages;

/// <summary>
/// One block instance in a page's block tree. Mirrors the frontend <c>BlockInstance</c>
/// (<c>frontend/src/lib/web-editor/core/types.ts</c>). The backend doesn't know block types:
/// <see cref="Name"/> is opaque and <see cref="Attributes"/> only holds primitive values.
/// </summary>
public class Block
{
	public required string Id { get; set; }
	public required string Name { get; set; }
	public Dictionary<string, JsonElement> Attributes { get; set; } = new();
	public List<Block> InnerBlocks { get; set; } = new();
}
