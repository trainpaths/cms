using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using api_backend.Services.Media;

namespace api_backend.Tests.Integration;

/// <summary>Test double for <see cref="IBlobStorage"/>.</summary>
public class InMemoryBlobStorage : IBlobStorage
{
	public ConcurrentDictionary<string, (byte[] Bytes, string ContentType)> Blobs { get; } = new();

	public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
	{
		using var ms = new MemoryStream();
		await content.CopyToAsync(ms, ct);
		Blobs[key] = (ms.ToArray(), contentType);
	}

	public Task<BlobObject?> GetAsync(string key, CancellationToken ct) =>
		Task.FromResult(Blobs.TryGetValue(key, out var blob)
			? new BlobObject(new MemoryStream(blob.Bytes), blob.ContentType, blob.Bytes.Length)
			: null);

	public async IAsyncEnumerable<string> ListKeysAsync([EnumeratorCancellation] CancellationToken ct)
	{
		foreach (var key in Blobs.Keys)
			yield return key;
		await Task.CompletedTask;
	}

	public Task DeleteAsync(string key, CancellationToken ct)
	{
		Blobs.TryRemove(key, out _);
		return Task.CompletedTask;
	}
}
