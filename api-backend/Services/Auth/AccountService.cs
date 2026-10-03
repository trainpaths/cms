using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using api_backend.Models.Auth;
using api_backend.Models.Auth.JWT;
using api_backend.Services.Auth.JWT;
using api_backend.Services.Email;

namespace api_backend.Services.Auth;

/// <summary>
/// Customer email verification and password reset. The "request" methods never reveal whether an
/// account exists; emails are queued so SMTP latency doesn't either.
/// </summary>
public class AccountService(
	AppDbContext db,
	PasswordHashService hasher,
	RefreshTokenService refreshTokens,
	IEmailQueue emails,
	IOptions<EmailOptions> emailOptions)
{
	private readonly EmailOptions _o = emailOptions.Value;

	private string LinkBaseUrl => _o.LinkBaseUrl.TrimEnd('/');

	public async Task RequestVerificationAsync(string rawEmail, CancellationToken ct = default)
	{
		var email = rawEmail.Trim().ToLowerInvariant();
		var customer = await db.Customers.FirstOrDefaultAsync(c => c.Email == email, ct);
		// Stay silent if the account is missing, inactive, or already confirmed.
		if (customer is null || !customer.IsActive || customer.IsEmailConfirmed) return;

		var raw = await IssueTokenAsync(
			customer, VerificationTokenPurpose.EmailVerification, _o.VerificationLinkExpiryHours, ct);

		var link = $"{LinkBaseUrl}/verify-email?token={raw}";
		emails.Enqueue(new EmailMessage(
			customer.Email,
			"Confirm your email",
			$"Please confirm your email address by opening this link:\n\n{link}\n\n" +
			$"The link expires in {_o.VerificationLinkExpiryHours} hour(s)."));
	}

	public async Task<bool> ConfirmEmailAsync(string rawToken, CancellationToken ct = default)
	{
		var token = await FindActiveTokenAsync(rawToken, VerificationTokenPurpose.EmailVerification, ct);
		if (token?.Customer is null) return false;

		var now = DateTimeOffset.UtcNow;
		token.ConsumedAt = now;
		token.Customer.IsEmailConfirmed = true;
		token.Customer.UpdatedAt = now;
		await db.SaveChangesAsync(ct);
		return true;
	}

	public async Task ForgotPasswordAsync(string rawEmail, CancellationToken ct = default)
	{
		var email = rawEmail.Trim().ToLowerInvariant();
		var customer = await db.Customers.FirstOrDefaultAsync(c => c.Email == email, ct);
		if (customer is null || !customer.IsActive) return; // silent — no enumeration

		var raw = await IssueTokenAsync(
			customer, VerificationTokenPurpose.PasswordReset, _o.ResetLinkExpiryHours, ct);

		var link = $"{LinkBaseUrl}/reset-password?token={raw}";
		emails.Enqueue(new EmailMessage(
			customer.Email,
			"Reset your password",
			$"Reset your password by opening this link:\n\n{link}\n\n" +
			$"The link expires in {_o.ResetLinkExpiryHours} hour(s). " +
			"If you didn't request this, you can ignore this email."));
	}

	public async Task<bool> ResetPasswordAsync(string rawToken, string newPassword, CancellationToken ct = default)
	{
		var token = await FindActiveTokenAsync(rawToken, VerificationTokenPurpose.PasswordReset, ct);
		if (token?.Customer is null) return false;

		var now = DateTimeOffset.UtcNow;
		token.ConsumedAt = now;
		token.Customer.PasswordHash = hasher.Hash(newPassword);
		// The reset link proves ownership of the address.
		token.Customer.IsEmailConfirmed = true;
		token.Customer.UpdatedAt = now;

		await refreshTokens.RevokeAllAsync(UserType.Customer, token.Customer.Id, ct);
		await db.SaveChangesAsync(ct);
		return true;
	}

	// Also invalidates earlier unused tokens of the same purpose.
	private async Task<string> IssueTokenAsync(
		Customer customer, VerificationTokenPurpose purpose, int expiryHours, CancellationToken ct)
	{
		var now = DateTimeOffset.UtcNow;
		await db.VerificationTokens
			.Where(t => t.CustomerId == customer.Id && t.Purpose == purpose
						&& t.ConsumedAt == null && t.ExpiresAt > now)
			.ExecuteUpdateAsync(s => s.SetProperty(t => t.ConsumedAt, now), ct);

		var generated = SecureToken.Create();
		db.VerificationTokens.Add(new VerificationToken
		{
			TokenHash = generated.TokenHash,
			Purpose = purpose,
			CustomerId = customer.Id,
			ExpiresAt = now.AddHours(expiryHours),
		});
		await db.SaveChangesAsync(ct);
		return generated.RawToken;
	}

	private async Task<VerificationToken?> FindActiveTokenAsync(
		string rawToken, VerificationTokenPurpose purpose, CancellationToken ct)
	{
		var hash = SecureToken.Hash(rawToken);
		var token = await db.VerificationTokens
			.Include(t => t.Customer)
			.FirstOrDefaultAsync(t => t.TokenHash == hash && t.Purpose == purpose, ct);
		return token is null || !token.IsActive ? null : token;
	}
}
