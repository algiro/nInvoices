using PuppeteerSharp;
using PuppeteerSharp.Media;
using nInvoices.Application.Services;

namespace nInvoices.Infrastructure.PdfExport;

/// <summary>
/// PDF converter using PuppeteerSharp (Chromium headless browser).
/// Provides high-fidelity HTML to PDF conversion with full CSS support including gradients, flexbox, and modern CSS.
/// </summary>
public sealed class PuppeteerPdfConverter : IHtmlToPdfConverter, IAsyncDisposable
{
    private IBrowser? _browser;
    private readonly SemaphoreSlim _browserLock = new(1, 1);
    private bool _isDisposed;

    public async Task<byte[]> ConvertAsync(string html, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(html);

        var browser = await GetBrowserAsync(cancellationToken);
        await using var page = await browser.NewPageAsync();

        // Set content and wait for it to load
        await page.SetContentAsync(html, new NavigationOptions
        {
            WaitUntil = [WaitUntilNavigation.Networkidle0]
        });

        // Generate PDF with appropriate settings
        var pdfData = await page.PdfDataAsync(new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true, // Essential for backgrounds and colors
            MarginOptions = new MarginOptions
            {
                Top = "0",
                Bottom = "0",
                Left = "0",
                Right = "0"
            }
        });

        return pdfData;
    }

    /// <summary>
    /// Downloads the headless Chrome build PuppeteerSharp expects, next to the application binaries,
    /// unless it is already there. Called at image build time (see docker/Dockerfile.api) so the first
    /// PDF request doesn't have to wait for a ~150 MB download.
    /// </summary>
    public static async Task EnsureBrowserDownloadedAsync()
    {
        var browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync();
    }

    private async Task<IBrowser> GetBrowserAsync(CancellationToken cancellationToken)
    {
        if (_browser != null)
            return _browser;

        await _browserLock.WaitAsync(cancellationToken);
        try
        {
            if (_browser != null)
                return _browser;

            await EnsureBrowserDownloadedAsync();

            // Launch browser in headless mode
            _browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                Args = ["--no-sandbox", "--disable-setuid-sandbox"]
            });

            return _browser;
        }
        finally
        {
            _browserLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        if (_browser != null)
        {
            await _browser.CloseAsync();
            _browser.Dispose();
        }

        _browserLock.Dispose();
    }
}
