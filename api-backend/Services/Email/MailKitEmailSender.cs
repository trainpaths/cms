using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace api_backend.Services.Email;

/// <summary>
/// Real SMTP delivery via MailKit. Registered when <c>Email:Host</c> is configured. Connects with
/// opportunistic STARTTLS and authenticates only when a user is set (anonymous relays are allowed).
/// </summary>
public class MailKitEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
	private readonly EmailOptions _o = options.Value;

	public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
	{
		var mime = new MimeMessage();
		mime.From.Add(new MailboxAddress(_o.FromName ?? _o.From, _o.From));
		mime.To.Add(MailboxAddress.Parse(message.To));
		mime.Subject = message.Subject;
		mime.Body = new TextPart("plain") { Text = message.Body };

		using var client = new SmtpClient();
		await client.ConnectAsync(_o.Host, _o.Port, SecureSocketOptions.StartTlsWhenAvailable, ct);
		if (!string.IsNullOrWhiteSpace(_o.User))
			await client.AuthenticateAsync(_o.User, _o.Password ?? string.Empty, ct);
		await client.SendAsync(mime, ct);
		await client.DisconnectAsync(true, ct);
	}
}
