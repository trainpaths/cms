namespace api_backend.Services.Email;

/// <summary>
/// Bound from the <c>Email</c> configuration section. When <see cref="Host"/> is empty the app
/// registers the <see cref="ConsoleEmailSender"/> (dev / no-SMTP); otherwise the
/// <see cref="MailKitEmailSender"/> connects to the configured SMTP server.
/// </summary>
public class EmailOptions
{
	public const string SectionName = "Email";

	/// <summary>SMTP host. Empty → console sender (links appear in the API logs).</summary>
	public string Host { get; set; } = string.Empty;
	public int Port { get; set; } = 587;
	public string? User { get; set; }
	public string? Password { get; set; }

	/// <summary>Envelope/from address used on every outgoing message.</summary>
	public string From { get; set; } = "no-reply@example.com";
	public string? FromName { get; set; }

	/// <summary>Frontend origin used to build the links embedded in emails (no trailing slash).</summary>
	public string LinkBaseUrl { get; set; } = "http://localhost:5173";

	public int VerificationLinkExpiryHours { get; set; } = 24;
	public int ResetLinkExpiryHours { get; set; } = 1;
}
