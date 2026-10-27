using api_backend.Models.Auth.JWT;
using api_backend.Models.Dto;
using api_backend.Services.Cms;
using api_backend.Services.Translation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace api_backend.Controllers;

/// <summary>
/// Machine translation for the editor ("Translate missing fields"): default-language text → a configured language.
/// Off (404) unless a DeepL key is set. Fills the working copy only; nothing is saved or published here.
/// </summary>
[ApiController]
[Route("api/translate")]
[Authorize(Policy = AuthPolicies.StaffOnly)]
[EnableRateLimiting(RateLimitPolicies.Pages)]
public class TranslateController(ITranslator translator, IOptions<TranslationOptions> options, CmsConfig cms)
	: ControllerBase
{
	public const long MaxBodyBytes = 512 * 1024;
	public const int MaxTexts = 500;
	public const int MaxTextLength = 10_000;

	[HttpPost]
	[RequestSizeLimit(MaxBodyBytes)]
	public async Task<ActionResult<TranslateResponse>> Translate(TranslateRequest req, CancellationToken ct)
	{
		if (!options.Value.Enabled)
			return Problem(statusCode: 404, title: "Machine translation is not configured.");
		var lang = cms.Site.Lang;
		if (!lang.IsPrefix(req.Target))
			return Problem(statusCode: 400, title: "Invalid translation request.",
				detail: $"'{req.Target}' is not a configured language other than the default.");
		if (req.Texts.Count > MaxTexts || req.Texts.Any(t => t is null || t.Length > MaxTextLength))
			return Problem(statusCode: 400, title: "Invalid translation request.",
				detail: $"At most {MaxTexts} texts of at most {MaxTextLength} characters each.");

		var targetTag = lang.Tags.First(t => CmsSiteLang.CodeOf(t) == req.Target);
		try
		{
			return Ok(new TranslateResponse([.. await translator.TranslateAsync(req.Texts, lang.Default, targetTag, ct)]));
		}
		catch (TranslationException ex)
		{
			return Problem(statusCode: ex.BadRequest ? 400 : 502, title: "Translation failed.", detail: ex.Message);
		}
	}
}
