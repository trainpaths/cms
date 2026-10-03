namespace api_backend.Models.Auth.JWT;

/// <summary>Custom claim types and authorization policy names used across the template.</summary>
public static class AuthClaims
{
	/// <summary>"customer" or "staff" — lets one middleware serve both principal kinds.</summary>
	public const string UserType = "user_type";

	/// <summary>Optional tenant/organisation scope carried for staff users.</summary>
	public const string OrganizationId = "org_id";

	public const string CustomerUserType = "customer";
	public const string StaffUserType = "staff";
}

public static class AuthPolicies
{
	public const string CustomerOnly = "CustomerOnly";
	public const string StaffOnly = "StaffOnly";
	public const string SuperAdmin = "SuperAdmin";
}

/// <summary>Rate-limiter policies, partitioned per client IP.</summary>
public static class RateLimitPolicies
{
	/// <summary>Credential and email endpoints.</summary>
	public const string Credentials = "auth-credentials";

	/// <summary>Session endpoints (refresh, logout, me, profile).</summary>
	public const string General = "auth";

	/// <summary>Page editor + public page reads; higher budget because the editor autosaves.</summary>
	public const string Pages = "pages";
}
