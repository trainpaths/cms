using api_backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace api_backend.Controllers;

/// <summary>A controller's ProblemDetails titles for <see cref="ServiceError"/>s (the detail comes from the service).</summary>
public sealed record ProblemTitles(string NotFound, string Invalid, string Conflict = "Conflict.");

public static class ServiceProblems
{
	/// <summary>404 (title only) / 409 / 400 with the result's detail.</summary>
	public static ObjectResult ToProblem<T>(this ControllerBase controller, ServiceResult<T> result, ProblemTitles titles) =>
		result.Error switch
		{
			ServiceError.NotFound => controller.NotFoundProblem(titles),
			ServiceError.Conflict => controller.Problem(statusCode: 409, title: titles.Conflict, detail: result.Detail),
			_ => controller.Problem(statusCode: 400, title: titles.Invalid, detail: result.Detail),
		};

	public static ObjectResult NotFoundProblem(this ControllerBase controller, ProblemTitles titles) =>
		controller.Problem(statusCode: 404, title: titles.NotFound);
}
