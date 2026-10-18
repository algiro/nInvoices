using FluentValidation;
using Mediator;
using nInvoices.Application.DTOs;
using nInvoices.Application.Localization;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Preferences;

/// <summary>The user's preferences (the app's language), with the defaults for a user who never changed them.</summary>
public sealed record GetUserPreferencesQuery : IRequest<UserPreferencesDto>;

/// <summary>Changes the user's preferences; they follow the user to every device.</summary>
public sealed record UpdateUserPreferencesCommand(UpdateUserPreferencesDto Preferences) : IRequest<UserPreferencesDto>;

public sealed class UpdateUserPreferencesDtoValidator : AbstractValidator<UpdateUserPreferencesDto>
{
    public UpdateUserPreferencesDtoValidator(IAppText text)
    {
        RuleFor(x => x.UiLanguage)
            .NotEmpty().WithMessage(_ => text.Translate("preferences.chooseLanguage"));
        RuleFor(x => x.UiLanguage)
            .Must(language => text.Match(language) is not null)
            .WithMessage(x => text.Translate("preferences.unsupportedLanguage",
                ("language", x.UiLanguage), ("languages", string.Join(", ", text.Languages))))
            .When(x => !string.IsNullOrWhiteSpace(x.UiLanguage));
    }
}

public sealed class UpdateUserPreferencesCommandValidator : AbstractValidator<UpdateUserPreferencesCommand>
{
    public UpdateUserPreferencesCommandValidator(IAppText text)
    {
        RuleFor(x => x.Preferences).NotNull().SetValidator(new UpdateUserPreferencesDtoValidator(text));
    }
}

public sealed class GetUserPreferencesQueryHandler : IRequestHandler<GetUserPreferencesQuery, UserPreferencesDto>
{
    private readonly IRepository<UserPreferences> _preferences;
    private readonly IAppText _text;

    public GetUserPreferencesQueryHandler(IRepository<UserPreferences> preferences, IAppText text)
    {
        _preferences = preferences;
        _text = text;
    }

    public async ValueTask<UserPreferencesDto> Handle(GetUserPreferencesQuery request, CancellationToken cancellationToken)
    {
        // The query filters return only the current user's row
        var preferences = (await _preferences.GetAllAsync(cancellationToken)).FirstOrDefault();
        return UserPreferencesMapper.ToDto(preferences, _text);
    }
}

public sealed class UpdateUserPreferencesCommandHandler : IRequestHandler<UpdateUserPreferencesCommand, UserPreferencesDto>
{
    private readonly IRepository<UserPreferences> _preferences;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppText _text;

    public UpdateUserPreferencesCommandHandler(IRepository<UserPreferences> preferences, IUnitOfWork unitOfWork, IAppText text)
    {
        _preferences = preferences;
        _unitOfWork = unitOfWork;
        _text = text;
    }

    public async ValueTask<UserPreferencesDto> Handle(UpdateUserPreferencesCommand request, CancellationToken cancellationToken)
    {
        // Checked by the validator: one of the app's languages, stored as the app names it ("IT" → "it")
        var language = _text.Match(request.Preferences.UiLanguage)!;

        var preferences = (await _preferences.GetAllAsync(cancellationToken)).FirstOrDefault();
        if (preferences is null)
        {
            preferences = new UserPreferences(language);
            await _preferences.AddAsync(preferences, cancellationToken);
        }
        else
        {
            preferences.ChangeUiLanguage(language);
            await _preferences.UpdateAsync(preferences, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return UserPreferencesMapper.ToDto(preferences, _text);
    }
}
