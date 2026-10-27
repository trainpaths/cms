using System.ComponentModel.DataAnnotations;

namespace api_backend.Models.Dto;

/// <summary><see cref="Target"/>: a configured non-default language code; the source is the default language.</summary>
public record TranslateRequest([Required, MaxLength(3)] string Target, [Required] List<string> Texts);

/// <summary>Same order and count as the request's texts.</summary>
public record TranslateResponse(List<string> Texts);
