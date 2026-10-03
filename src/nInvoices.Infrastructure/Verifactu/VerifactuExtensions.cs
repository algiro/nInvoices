using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using nInvoices.Core.Configuration;
using nInvoices.Application.Compliance.Spain.Verifactu;

namespace nInvoices.Infrastructure.Verifactu;

public static class VerifactuExtensions
{
    /// <summary>
    /// Registers the Tax Agency client and the worker that sends the waiting Verifactu records. The
    /// worker does nothing until Compliance:Spain:Verifactu is set up on the server.
    /// </summary>
    public static IServiceCollection AddVerifactuSubmission(this IServiceCollection services)
    {
        services.AddSingleton<IAeatVerifactuClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<VerifactuOptions>>().Value;
            return new AeatVerifactuClient(addressOf: target =>
                new Uri(string.IsNullOrWhiteSpace(options.ServiceUrl) ? target.Url : options.ServiceUrl));
        });
        services.AddHostedService<VerifactuSubmissionWorker>();
        return services;
    }
}
