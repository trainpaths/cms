using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using api_backend.Services.Email;

namespace api_backend.Tests.Integration;

/// <summary>
/// Test <see cref="IEmailSender"/> that captures messages instead of sending them, so tests can
/// recover the emailed link/token — the raw token is never persisted (only its hash is stored).
/// </summary>
public partial class CollectingEmailSender : IEmailSender
{
	private readonly ConcurrentQueue<EmailMessage> _messages = new();

	public Task SendAsync(EmailMessage message, CancellationToken ct = default)
	{
		_messages.Enqueue(message);
		return Task.CompletedTask;
	}

	/// <summary>The token from the most recent email sent to <paramref name="to"/>, or null.</summary>
	public string? LatestTokenFor(string to)
	{
		var message = _messages.LastOrDefault(m => string.Equals(m.To, to, StringComparison.OrdinalIgnoreCase));
		if (message is null) return null;
		var match = TokenRegex().Match(message.Body);
		return match.Success ? match.Groups[1].Value : null;
	}

	/// <summary>Waits (emails are sent in the background) for a token other than <paramref name="previousToken"/>.</summary>
	public async Task<string?> WaitForTokenAsync(string to, string? previousToken = null, CancellationToken ct = default)
	{
		var deadline = DateTime.UtcNow.AddSeconds(5);
		while (DateTime.UtcNow < deadline)
		{
			var token = LatestTokenFor(to);
			if (token is not null && token != previousToken) return token;
			await Task.Delay(25, ct);
		}
		return null;
	}

	[GeneratedRegex(@"token=([A-Za-z0-9_-]+)")]
	private static partial Regex TokenRegex();
}
