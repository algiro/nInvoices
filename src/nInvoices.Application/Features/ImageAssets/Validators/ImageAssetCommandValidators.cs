using FluentValidation;

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

    public ImageFileValidator()
    {
        RuleFor(x => x.Content.LongLength)
            .LessThanOrEqualTo(MaxFileSizeBytes)
            .WithMessage($"File size exceeds the maximum of {MaxFileSizeBytes / 1024 / 1024} MB.")
            .OverridePropertyName("Size");

        RuleFor(x => x.ContentType)
            .Must(AllowedContentTypes.Contains)
            .WithMessage(x => $"Content type '{x.ContentType}' is not allowed. Allowed: {string.Join(", ", AllowedContentTypes)}");
    }
}

public sealed class UploadImageAssetCommandValidator : AbstractValidator<UploadImageAssetCommand>
{
    public UploadImageAssetCommandValidator()
    {
        RuleFor(x => x.Alias).NotEmpty().WithMessage("Alias is required.");

        RuleFor(x => x.File)
            .Must(f => f is { Content.Length: > 0 }).WithMessage("File is required.");
        RuleFor(x => x.File!)
            .SetValidator(new ImageFileValidator())
            .When(x => x.File is { Content.Length: > 0 });
    }
}

public sealed class UpdateImageAssetCommandValidator : AbstractValidator<UpdateImageAssetCommand>
{
    public UpdateImageAssetCommandValidator()
    {
        // A file is optional on update; none (or an empty one) keeps the current image
        RuleFor(x => x.File!)
            .SetValidator(new ImageFileValidator())
            .When(x => x.File is { Content.Length: > 0 });
    }
}
