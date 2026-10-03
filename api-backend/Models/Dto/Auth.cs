using System.ComponentModel.DataAnnotations;

namespace api_backend.Models.Dto;

// Max lengths match the DB columns and cap PBKDF2 input.

public record RegisterRequest(
	[Required, EmailAddress, MaxLength(256)] string Email,
	[Required, MinLength(8), MaxLength(128)] string Password,
	[MaxLength(128)] string? DisplayName);

public record StaffRegisterRequest(
	[Required, EmailAddress, MaxLength(256)] string Email,
	[Required, MinLength(8), MaxLength(128)] string Password,
	[MaxLength(128)] string? DisplayName,
	[MaxLength(128)] string? OrganizationId,
	[MaxLength(16)] string[]? Roles);

public record LoginRequest(
	[Required, EmailAddress, MaxLength(256)] string Email,
	[Required, MaxLength(128)] string Password);

/// <summary>The refresh token is sent as an HttpOnly <c>refresh_token</c> cookie, not in the body.</summary>
public record AuthResponse(
	string AccessToken,
	DateTimeOffset AccessTokenExpiresAt,
	UserInfo User);

public record UserInfo(
	Guid Id,
	string Email,
	string? DisplayName,
	string UserType,
	string[] Roles,
	string? OrganizationId);

public record ChangePasswordRequest(
	[Required, MaxLength(128)] string CurrentPassword,
	[Required, MinLength(8), MaxLength(128)] string NewPassword);

public record UpdateProfileRequest(
	[MaxLength(128)] string? DisplayName);

public record RequestVerificationRequest(
	[Required, EmailAddress, MaxLength(256)] string Email);

public record ConfirmEmailRequest(
	[Required, MaxLength(512)] string Token);

public record ForgotPasswordRequest(
	[Required, EmailAddress, MaxLength(256)] string Email);

public record ResetPasswordRequest(
	[Required, MaxLength(512)] string Token,
	[Required, MinLength(8), MaxLength(128)] string NewPassword);
