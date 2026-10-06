namespace api_backend.Services.Backup;

public class BackupOptions
{
	public const string SectionName = "Backup";

	/// <summary>Where archives live inside the container; the instance's compose file mounts a host dir here.</summary>
	public string Directory { get; set; } = "/backups";

	/// <summary>The host side of that mount (<c>BACKUP_DIR</c>), only shown in the admin. The API can't see it.</summary>
	public string? HostPath { get; set; }

	public long MaxUploadMegabytes { get; set; } = 2048;
}

/// <summary>
/// Serializes backup work: one backup, restore, upload or delete at a time. While <see cref="Restoring"/>, the
/// maintenance middleware answers every other API request with 503.
/// </summary>
public sealed class BackupLock
{
	private readonly SemaphoreSlim _semaphore = new(1, 1);
	private volatile bool _restoring;

	public bool Restoring => _restoring;

	/// <summary>Null when busy; dispose the result to release.</summary>
	public IDisposable? TryEnter() => _semaphore.Wait(0) ? new Releaser(this) : null;

	/// <summary>Within a held lock: the maintenance gate on until disposed.</summary>
	public IDisposable Maintenance()
	{
		_restoring = true;
		return new Gate(this);
	}

	private sealed class Releaser(BackupLock owner) : IDisposable
	{
		private int _disposed;

		public void Dispose()
		{
			if (Interlocked.Exchange(ref _disposed, 1) == 0)
				owner._semaphore.Release();
		}
	}

	private sealed class Gate(BackupLock owner) : IDisposable
	{
		public void Dispose() => owner._restoring = false;
	}
}
