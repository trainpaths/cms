using System.Threading.Channels;

namespace api_backend.Services.Email;

public interface IEmailQueue
{
	void Enqueue(EmailMessage message);
}

/// <summary>In-memory queue drained by <see cref="EmailDispatcher"/>; unsent mail is lost on shutdown.</summary>
public sealed class EmailQueue(ILogger<EmailQueue> logger) : IEmailQueue
{
	private const int Capacity = 1000;

	private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(
		new BoundedChannelOptions(Capacity) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

	public ChannelReader<EmailMessage> Reader => _channel.Reader;

	public void Enqueue(EmailMessage message)
	{
		if (!_channel.Writer.TryWrite(message))
			logger.LogError("Email queue is full; dropping message \"{Subject}\".", message.Subject);
	}
}

public sealed class EmailDispatcher(
	EmailQueue queue,
	IEmailSender sender,
	ILogger<EmailDispatcher> logger) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken))
		{
			try
			{
				await sender.SendAsync(message, stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed to send email \"{Subject}\".", message.Subject);
			}
		}
	}
}
