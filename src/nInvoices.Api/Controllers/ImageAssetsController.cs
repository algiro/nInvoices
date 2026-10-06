using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.ImageAssets;

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
    // Above the 1 MB an image may be (ImageFileValidator), to leave room for the multipart overhead
    private const long MaxRequestBytes = 2_097_152;

    private readonly IMediator _mediator;

    public ImageAssetsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Gets all image assets (metadata only, no base64 data).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ImageAssetDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ImageAssetDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetImageAssetsQuery(), cancellationToken));

    /// <summary>
    /// Gets an image asset by ID (includes base64 data).
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ImageAssetWithDataDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImageAssetWithDataDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var asset = await _mediator.Send(new GetImageAssetByIdQuery(id), cancellationToken);
        return asset is null ? NotFound() : Ok(asset);
    }

    /// <summary>
    /// Uploads a new image asset (at most 1 MB; PNG, JPEG, GIF, SVG or WebP).
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(ImageAssetDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImageAssetDto>> Upload(
        [FromForm] string alias,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var asset = await _mediator.Send(new UploadImageAssetCommand(alias, await ReadAsync(file, cancellationToken)), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = asset.Id }, asset);
    }

    /// <summary>
    /// Updates an image asset's alias or replaces the image file.
    /// </summary>
    [HttpPut("{id:long}")]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(ImageAssetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImageAssetDto>> Update(
        long id,
        [FromForm] string? alias,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        var asset = await _mediator.Send(new UpdateImageAssetCommand(id, alias, await ReadAsync(file, cancellationToken)), cancellationToken);
        return asset is null ? NotFound() : Ok(asset);
    }

    /// <summary>
    /// Deletes an image asset.
    /// </summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken) =>
        await _mediator.Send(new DeleteImageAssetCommand(id), cancellationToken) ? NoContent() : NotFound();

    private static async Task<ImageFile?> ReadAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null)
            return null;

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);
        return new ImageFile(file.FileName, file.ContentType, memoryStream.ToArray());
    }
}
