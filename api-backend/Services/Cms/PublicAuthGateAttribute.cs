using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace api_backend.Services.Cms;

/// <summary>
/// 404 for every action of the controller unless the instance config enables customer accounts
/// (<see cref="CmsConfig.PublicAuth"/>). A filter, not a removed controller: the endpoints stay in the OpenAPI
/// contract, which is generated without an instance config. [Authorize] still runs first (middleware), so
/// authenticated endpoints answer 401 to anonymous callers.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class PublicAuthGateAttribute : Attribute, IResourceFilter
{
	public void OnResourceExecuting(ResourceExecutingContext context)
	{
		if (!context.HttpContext.RequestServices.GetRequiredService<CmsConfig>().PublicAuth)
			context.Result = new NotFoundResult();
	}

	public void OnResourceExecuted(ResourceExecutedContext context) { }
}
