using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace api_backend;

public class GlobalExceptionHandler(
	IHostEnvironment env,
	ILogger<GlobalExceptionHandler> logger,
	IProblemDetailsService problemDetailsService) : IExceptionHandler
{
	public async ValueTask<bool> TryHandleAsync(
		HttpContext httpContext,
		Exception exception,
		CancellationToken cancellationToken)
	{
		logger.LogError(exception, "Unhandled exception");

		var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

		var problem = new ProblemDetails
		{
			Status = StatusCodes.Status500InternalServerError,
			Title = "An unexpected error occurred.",
			Instance = httpContext.Request.Path,
		};
		problem.Extensions["traceId"] = traceId;

		if (env.IsDevelopment())
		{
			problem.Detail = exception.ToString();
		}

		httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

		return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
		{
			HttpContext = httpContext,
			ProblemDetails = problem,
		});
	}
}
