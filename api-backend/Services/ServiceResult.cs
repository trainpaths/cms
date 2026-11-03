namespace api_backend.Services;

/// <summary>Why a service operation failed; controllers map it to a ProblemDetails status (404 / 409 / 400).</summary>
public enum ServiceError
{
	NotFound,

	/// <summary>Clashes with existing state: slug or menu handle taken, another backup operation running.</summary>
	Conflict,

	Invalid,
}

/// <summary>A service operation's value, or its <see cref="ServiceError"/> plus a user-facing <see cref="Detail"/>.</summary>
public record ServiceResult<T>(T? Value, ServiceError? Error = null, string? Detail = null)
{
	public static ServiceResult<T> Ok(T value) => new(value);
	public static ServiceResult<T> Fail(ServiceError error, string? detail = null) => new(default, error, detail);
}
