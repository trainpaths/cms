namespace api_backend.Services.Media;

/// <summary>A stored object opened for reading. Dispose to release the underlying connection.</summary>
public sealed record BlobObject(Stream Content, string ContentType, long Length) : IAsyncDisposable
{
	public ValueTask DisposeAsync() => Content.DisposeAsync();
}

/// <summary>
/// Provider-agnostic object storage for uploaded media. Keys are flat (<c>{guid}.{ext}</c>).
/// Production implementation: <see cref="S3BlobStorage"/> (SeaweedFS in the compose stack, or any S3 API).
/// </summary>
public interface IBlobStorage
{
	Task PutAsync(string key, Stream content, string contentType, CancellationToken ct);

	/// <summary>Null when the key doesn't exist.</summary>
	Task<BlobObject?> GetAsync(string key, CancellationToken ct);

	/// <summary>Idempotent: deleting a missing key succeeds.</summary>
	Task DeleteAsync(string key, CancellationToken ct);

	/// <summary>Every key in the store (a backup restore removes the ones no media row references).</summary>
	IAsyncEnumerable<string> ListKeysAsync(CancellationToken ct);
}
