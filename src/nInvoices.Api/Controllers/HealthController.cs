using Microsoft.AspNetCore.Mvc;

namespace nInvoices.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;
    private readonly TimeProvider _timeProvider;

    public HealthController(ILogger<HealthController> logger, TimeProvider timeProvider)
    {
        _logger = logger;
        _timeProvider = timeProvider;
    }

    [HttpGet]
    public IActionResult Get()
    {
        _logger.LogInformation("Health check requested");
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
            Version = "1.0.0",
            Application = "nInvoices API"
        });
    }
}
