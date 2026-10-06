using Mediator;
using Microsoft.Extensions.Logging;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Exceptions;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.ImageAssets;

/// <summary>An uploaded image file, as read from the request.</summary>
public sealed record ImageFile(string FileName, string ContentType, byte[] Content);

/// <summary>The image assets, without their data. Templates print one with <c>[[ Image "alias" 200 80 ]]</c>.</summary>
public sealed record GetImageAssetsQuery : IRequest<IReadOnlyList<ImageAssetDto>>;

/// <summary>An image asset with its base64 data; null when it doesn't exist.</summary>
public sealed record GetImageAssetByIdQuery(long Id) : IRequest<ImageAssetWithDataDto?>;

/// <summary>Stores a new image under an alias, unique among the user's images.</summary>
public sealed record UploadImageAssetCommand(string? Alias, ImageFile? File) : IRequest<ImageAssetDto>;

/// <summary>Renames an image and/or replaces its file (null = unchanged); null when it doesn't exist.</summary>
public sealed record UpdateImageAssetCommand(long Id, string? Alias, ImageFile? File) : IRequest<ImageAssetDto?>;

/// <summary>Deletes an image asset; false when it doesn't exist.</summary>
public sealed record DeleteImageAssetCommand(long Id) : IRequest<bool>;

public sealed class ImageAssetHandlers :
    IRequestHandler<GetImageAssetsQuery, IReadOnlyList<ImageAssetDto>>,
    IRequestHandler<GetImageAssetByIdQuery, ImageAssetWithDataDto?>,
    IRequestHandler<UploadImageAssetCommand, ImageAssetDto>,
    IRequestHandler<UpdateImageAssetCommand, ImageAssetDto?>,
    IRequestHandler<DeleteImageAssetCommand, bool>
{
    private readonly IRepository<ImageAsset> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ImageAssetHandlers> _logger;

    public ImageAssetHandlers(IRepository<ImageAsset> repository, IUnitOfWork unitOfWork, ILogger<ImageAssetHandlers> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async ValueTask<IReadOnlyList<ImageAssetDto>> Handle(GetImageAssetsQuery request, CancellationToken cancellationToken) =>
        (await _repository.GetAllAsync(cancellationToken)).Select(ImageAssetMapper.ToDto).ToList();

    public async ValueTask<ImageAssetWithDataDto?> Handle(GetImageAssetByIdQuery request, CancellationToken cancellationToken)
    {
        var asset = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return asset is null ? null : ImageAssetMapper.ToDtoWithData(asset);
    }

    public async ValueTask<ImageAssetDto> Handle(UploadImageAssetCommand request, CancellationToken cancellationToken)
    {
        // Alias and file are checked by UploadImageAssetCommandValidator
        var alias = request.Alias!;
        var file = request.File!;
        await EnsureAliasIsFreeAsync(alias, exceptId: null, cancellationToken);

        var asset = new ImageAsset(alias, file.FileName, file.ContentType, Convert.ToBase64String(file.Content), file.Content.LongLength);
        await _repository.AddAsync(asset, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Image asset '{Alias}' uploaded ({Size} bytes)", alias, file.Content.LongLength);
        return ImageAssetMapper.ToDto(asset);
    }

    public async ValueTask<ImageAssetDto?> Handle(UpdateImageAssetCommand request, CancellationToken cancellationToken)
    {
        var asset = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (asset is null)
            return null;

        if (!string.IsNullOrWhiteSpace(request.Alias) && request.Alias != asset.Alias)
        {
            await EnsureAliasIsFreeAsync(request.Alias, exceptId: asset.Id, cancellationToken);
            asset.UpdateAlias(request.Alias);
        }

        if (request.File is { Content.Length: > 0 } file)
            asset.UpdateImage(file.FileName, file.ContentType, Convert.ToBase64String(file.Content), file.Content.LongLength);

        await _repository.UpdateAsync(asset, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ImageAssetMapper.ToDto(asset);
    }

    public async ValueTask<bool> Handle(DeleteImageAssetCommand request, CancellationToken cancellationToken)
    {
        var asset = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (asset is null)
            return false;

        await _repository.DeleteAsync(asset, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Image asset '{Alias}' deleted", asset.Alias);
        return true;
    }

    private async Task EnsureAliasIsFreeAsync(string alias, long? exceptId, CancellationToken cancellationToken)
    {
        var taken = await _repository.FindAsync(a => a.Alias == alias && a.Id != exceptId, cancellationToken);
        if (taken.Any())
            throw new DomainException($"An image with alias '{alias}' already exists.");
    }
}
