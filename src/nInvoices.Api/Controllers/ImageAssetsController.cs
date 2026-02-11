using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Api.Controllers;

/// <summary>
/// API controller for managing image assets used in invoice templates.
/// Images are stored as base64 in the database and referenced by alias in templates.
/// Template usage: [[ Image "alias" 200 80 ]]
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ImageAssetsController : ControllerBase
{
    private readonly IRepository<ImageAsset> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ImageAssetsController> _logger;

    private const long MaxFileSizeBytes = 1_048_576; // 1 MB

    private static readonly HashSet<string> AllowedContentTypes =
    [
        "image/png",
        "image/jpeg",
        "image/gif",
        "image/svg+xml",
        "image/webp"
    ];

    public ImageAssetsController(
        IRepository<ImageAsset> repository,
        IUnitOfWork unitOfWork,
        ILogger<ImageAssetsController> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Gets all image assets (metadata only, no base64 data).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ImageAssetDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ImageAssetDto>>> GetAll(CancellationToken cancellationToken)
    {
        var assets = await _repository.GetAllAsync(cancellationToken);
        var dtos = assets.Select(a => new ImageAssetDto(
            a.Id, a.Alias, a.FileName, a.ContentType, a.FileSize,
            a.CreatedAt, a.UpdatedAt ?? a.CreatedAt));
        return Ok(dtos);
    }

    /// <summary>
    /// Gets an image asset by ID (includes base64 data).
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ImageAssetWithDataDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImageAssetWithDataDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var asset = await _repository.GetByIdAsync(id, cancellationToken);
        if (asset == null)
            return NotFound();

        return Ok(new ImageAssetWithDataDto(
            asset.Id, asset.Alias, asset.FileName, asset.ContentType,
            asset.Base64Data, asset.FileSize,
            asset.CreatedAt, asset.UpdatedAt ?? asset.CreatedAt));
    }

    /// <summary>
    /// Uploads a new image asset.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ImageAssetDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(2_097_152)] // 2 MB to account for multipart overhead
    public async Task<ActionResult<ImageAssetDto>> Upload(
        [FromForm] string alias,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(alias))
            return BadRequest(new { error = "Alias is required." });

        if (file == null || file.Length == 0)
            return BadRequest(new { error = "File is required." });

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(new { error = $"File size exceeds the maximum of {MaxFileSizeBytes / 1024 / 1024} MB." });

        if (!AllowedContentTypes.Contains(file.ContentType))
            return BadRequest(new { error = $"Content type '{file.ContentType}' is not allowed. Allowed: {string.Join(", ", AllowedContentTypes)}" });

        // Check alias uniqueness
        var existing = await _repository.FindAsync(a => a.Alias == alias, cancellationToken);
        if (existing.Any())
            return BadRequest(new { error = $"An image with alias '{alias}' already exists." });

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);
        var base64Data = Convert.ToBase64String(memoryStream.ToArray());

        var asset = new ImageAsset(alias, file.FileName, file.ContentType, base64Data, file.Length);

        await _repository.AddAsync(asset, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Image asset '{Alias}' uploaded ({Size} bytes)", alias, file.Length);

        var dto = new ImageAssetDto(
            asset.Id, asset.Alias, asset.FileName, asset.ContentType, asset.FileSize,
            asset.CreatedAt, asset.UpdatedAt ?? asset.CreatedAt);

        return CreatedAtAction(nameof(GetById), new { id = asset.Id }, dto);
    }

    /// <summary>
    /// Updates an image asset's alias or replaces the image file.
    /// </summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ImageAssetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(2_097_152)]
    public async Task<ActionResult<ImageAssetDto>> Update(
        long id,
        [FromForm] string? alias,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        var asset = await _repository.GetByIdAsync(id, cancellationToken);
        if (asset == null)
            return NotFound();

        if (!string.IsNullOrWhiteSpace(alias) && alias != asset.Alias)
        {
            var existing = await _repository.FindAsync(a => a.Alias == alias && a.Id != id, cancellationToken);
            if (existing.Any())
                return BadRequest(new { error = $"An image with alias '{alias}' already exists." });

            asset.UpdateAlias(alias);
        }

        if (file is { Length: > 0 })
        {
            if (file.Length > MaxFileSizeBytes)
                return BadRequest(new { error = $"File size exceeds the maximum of {MaxFileSizeBytes / 1024 / 1024} MB." });

            if (!AllowedContentTypes.Contains(file.ContentType))
                return BadRequest(new { error = $"Content type '{file.ContentType}' is not allowed." });

            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream, cancellationToken);
            var base64Data = Convert.ToBase64String(memoryStream.ToArray());

            asset.UpdateImage(file.FileName, file.ContentType, base64Data, file.Length);
        }

        await _repository.UpdateAsync(asset, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new ImageAssetDto(
            asset.Id, asset.Alias, asset.FileName, asset.ContentType, asset.FileSize,
            asset.CreatedAt, asset.UpdatedAt ?? asset.CreatedAt);

        return Ok(dto);
    }

    /// <summary>
    /// Deletes an image asset.
    /// </summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var asset = await _repository.GetByIdAsync(id, cancellationToken);
        if (asset == null)
            return NotFound();

        await _repository.DeleteAsync(asset, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Image asset '{Alias}' deleted", asset.Alias);

        return NoContent();
    }
}
