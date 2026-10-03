using Microsoft.Extensions.Logging;

namespace api_backend.Services.Email;

/// <summary>
/// Default sender: writes the rendered email to the logger instead of delivering it. Lets the
/// verification and password-reset flows be driven end-to-end (and the link copied) without SMTP.
/// </summary>
public class ConsoleEmailSender(ILogger<ConsoleEmailSender> logger) : IEmailSender
{
	public Task SendAsync(EmailMessage message, CancellationToken ct = default)
	{
		logger.LogInformation(
			"Email (console sender) → To: {To} | Subject: {Subject}\n{Body}",
			message.To, message.Subject, message.Body);
		return Task.CompletedTask;
	}
}
