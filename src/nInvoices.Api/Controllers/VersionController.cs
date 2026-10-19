using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nInvoices.Api.Infrastructure;
using nInvoices.Application.DTOs;

namespace nInvoices.Api.Controllers;

/// <summary>Which build is running, to match it with the source code.</summary>
[ApiController]
[Route("api/version")]
[Authorize]
public sealed class VersionController : ControllerBase
{
    /// <summary>The release number and the commit the API was built from.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(AppVersionDto), StatusCodes.Status200OK)]
    public ActionResult<AppVersionDto> Get() => Ok(BuildInfo.Current);
}
