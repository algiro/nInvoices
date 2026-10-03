using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nInvoices.Application.Compliance.Spain.Verifactu;
using nInvoices.Core.Configuration;
using nInvoices.Core.Entities;
using nInvoices.Infrastructure.Data;
using nInvoices.Infrastructure.Services;

namespace nInvoices.Infrastructure.Verifactu;

/// <summary>
/// Sends the Verifactu records that are waiting, for every user who has any, once a minute or so. It
/// runs each user submission as that user, so their certificate and data are the ones used. Idle (and
/// harmless) until Verifactu is set up on the server.
/// </summary>
public sealed class VerifactuSubmissionWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopes;
    private readonly VerifactuOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<VerifactuSubmissionWorker> _logger;

    public VerifactuSubmissionWorker(
        IServiceScopeFactory scopes,
        IOptions<VerifactuOptions> options,
        TimeProvider time,
        ILogger<VerifactuSubmissionWorker> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var missing = _options.Missing();
        if (missing.Count > 0)
        {
            _logger.LogInformation("Verifactu submission is idle: not set up ({Missing})", string.Join(", ", missing));
            return;
        }

        _logger.LogInformation("Verifactu records are sent to the Tax Agency ({Environment})", _options.IsProduction ? "production" : "test");

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); // let the application finish starting
            using var timer = new PeriodicTimer(Interval);
            do
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Sending Verifactu records failed");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    /// <summary>One pass over the users who have records due to be sent.</summary>
    /// <returns>How many users were attended.</returns>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        List<string> owners;
        using (var scope = _scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = _time.GetUtcNow().UtcDateTime;

            // Across all users, so the per-user filter is lifted here and only here
            owners = await db.VerifactuSubmissions
                .IgnoreQueryFilters()
                .Where(s => s.Status == VerifactuSubmissionStatus.Pending && s.NextAttemptAt <= now)
                .Select(s => s.OwnerId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        foreach (var owner in owners)
        {
            using var scope = _scopes.CreateScope();
            scope.ServiceProvider.GetRequiredService<OwnerOverride>().OwnerId = owner;

            var run = await scope.ServiceProvider.GetRequiredService<IVerifactuSubmitter>().SubmitPendingAsync(cancellationToken);
            if (run.Sent > 0 || run.Problem is not null)
            {
                _logger.LogInformation(
                    "Verifactu, user {Owner}: {Sent} sent, {Accepted} accepted, {WithErrors} with errors, {Rejected} rejected{Problem}",
                    owner, run.Sent, run.Accepted, run.AcceptedWithErrors, run.Rejected, run.Problem is null ? "" : $" ({run.Problem})");
            }
        }

        return owners.Count;
    }
}
