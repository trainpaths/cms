namespace api_backend.Services.Email;

/// <summary>
/// Provider-agnostic email seam. The default <see cref="ConsoleEmailSender"/> logs the rendered
/// message so the whole verification/reset flow can be exercised without SMTP; <see
/// cref="MailKitEmailSender"/> is selected when SMTP config (<c>Email:Host</c>) is present.
/// </summary>
public interface IEmailSender
{
	Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
