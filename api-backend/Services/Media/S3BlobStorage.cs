using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace api_backend.Services.Media;

public class S3StorageOptions
{
	public const string SectionName = "Storage:S3";

	/// <summary>S3 endpoint, e.g. <c>http://seaweedfs:8333</c>. Empty → storage not configured.</summary>
	public string ServiceUrl { get; set; } = "";
	public string AccessKey { get; set; } = "";
	public string SecretKey { get; set; } = "";
	public string Bucket { get; set; } = "media";
	public string Region { get; set; } = "us-east-1";
}

/// <summary>
/// <see cref="IBlobStorage"/> over any S3-compatible API (path-style addressing).
/// The bucket is created on first write, so a fresh SeaweedFS volume needs no manual setup.
/// </summary>
public sealed class S3BlobStorage : IBlobStorage, IDisposable
{
	private readonly AmazonS3Client _client;
	private readonly string _bucket;
	private readonly SemaphoreSlim _bucketLock = new(1, 1);
	private volatile bool _bucketReady;

	public S3BlobStorage(IOptions<S3StorageOptions> options)
	{
		var o = options.Value;
		_bucket = o.Bucket;
		_client = new AmazonS3Client(
			new BasicAWSCredentials(o.AccessKey, o.SecretKey),
			new AmazonS3Config
			{
				ServiceURL = o.ServiceUrl,
				AuthenticationRegion = o.Region,
				ForcePathStyle = true,
				// SDK v4 defaults to trailing CRC checksums; not every S3-compatible store handles them
				RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
				ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
			});
	}

	public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
	{
		await EnsureBucketAsync(ct);
		await _client.PutObjectAsync(new PutObjectRequest
		{
			BucketName = _bucket,
			Key = key,
			InputStream = content,
			ContentType = contentType,
			AutoCloseStream = false,
		}, ct);
	}

	public async Task<BlobObject?> GetAsync(string key, CancellationToken ct)
	{
		try
		{
			var response = await _client.GetObjectAsync(_bucket, key, ct);
			return new BlobObject(response.ResponseStream, response.Headers.ContentType, response.ContentLength);
		}
		catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}
	}

	public async Task DeleteAsync(string key, CancellationToken ct) =>
		await _client.DeleteObjectAsync(_bucket, key, ct);

	private async Task EnsureBucketAsync(CancellationToken ct)
	{
		if (_bucketReady)
			return;
		await _bucketLock.WaitAsync(ct);
		try
		{
			if (_bucketReady)
				return;
			try
			{
				await _client.PutBucketAsync(_bucket, ct);
			}
			catch (AmazonS3Exception e) when (e.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
			{
			}
			_bucketReady = true;
		}
		finally
		{
			_bucketLock.Release();
		}
	}

	public void Dispose()
	{
		_client.Dispose();
		_bucketLock.Dispose();
	}
}
