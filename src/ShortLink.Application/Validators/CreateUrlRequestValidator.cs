using FluentValidation;
using ShortLink.Application.Common.Interfaces;
using ShortLink.Application.DTOs;

namespace ShortLink.Application.Validators;

/// <summary>
/// FluentValidation rules for CreateUrlRequestDto.
/// Validates URL syntax, protocol requirement (http/https), and expiration constraints.
/// </summary>
public class CreateUrlRequestValidator : AbstractValidator<CreateUrlRequestDto>
{
    public CreateUrlRequestValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleFor(x => x.OriginalUrl)
            .NotEmpty()
            .WithMessage("Original URL is required.")
            .MaximumLength(2048)
            .WithMessage("URL length cannot exceed 2048 characters.")
            .Must(BeAValidHttpOrHttpsUrl)
            .WithMessage("Original URL must be a valid absolute HTTP or HTTPS URL.");

        RuleFor(x => x.ExpiresAt)
            .Must(expiresAt => !expiresAt.HasValue || expiresAt.Value > dateTimeProvider.UtcNow)
            .WithMessage("Expiration date must be in the future (UTC).");
    }

    private static bool BeAValidHttpOrHttpsUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult) &&
               (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }
}
