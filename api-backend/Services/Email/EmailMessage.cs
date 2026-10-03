namespace api_backend.Services.Email;

/// <summary>
/// A plain-text email to be delivered by an <see cref="IEmailSender"/>.
/// Kept deliberately minimal — the template ships text-only emails (no HTML templating engine).
/// </summary>
public record EmailMessage(string To, string Subject, string Body);
