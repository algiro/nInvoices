using FluentValidation;
using nInvoices.Application.Localization;
using nInvoices.Application.Validation;

namespace nInvoices.Application.Features.ImageAssets.Validators;

/// <summary>What an image used in templates may be: at most 1 MB, in a web image format.</summary>
public sealed class ImageFileValidator : AbstractValidator<ImageFile>
{
    public const long MaxFileSizeBytes = 1_048_576; // 1 MB

    public static readonly IReadOnlySet<string> AllowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/gif",
        "image/svg+xml",
        "image/webp"
    };

    public ImageFileValidator(IAppText text)
    {
        RuleFor(x => x.Content.LongLength)
            .LessThanOrEqualTo(MaxFileSizeBytes)
            .WithText(text, "validation.image.tooLarge", ("max", MaxFileSizeBytes / 1024 / 1024))
            .OverridePropertyName("Size");

        RuleFor(x => x.ContentType)
            .Must(AllowedContentTypes.Contains)
            .WithMessage(x => text.Translate("validation.image.typeNotAllowed", ("type", x.ContentType), ("allowed", string.Join(", ", AllowedContentTypes))));
    }
}

public sealed class UploadImageAssetCommandValidator : AbstractValidator<UploadImageAssetCommand>
{
    public UploadImageAssetCommandValidator(IAppText text)
    {
        RuleFor(x => x.Alias).NotEmpty().WithText(text, "validation.image.aliasRequired");

        RuleFor(x => x.File)
            .Must(f => f is { Content.Length: > 0 }).WithText(text, "validation.image.fileRequired");
        RuleFor(x => x.File!)
            .SetValidator(new ImageFileValidator(text))
            .When(x => x.File is { Content.Length: > 0 });
    }
}

public sealed class UpdateImageAssetCommandValidator : AbstractValidator<UpdateImageAssetCommand>
{
    public UpdateImageAssetCommandValidator(IAppText text)
    {
        // A file is optional on update; none (or an empty one) keeps the current image
        RuleFor(x => x.File!)
            .SetValidator(new ImageFileValidator(text))
            .When(x => x.File is { Content.Length: > 0 });
    }
}
