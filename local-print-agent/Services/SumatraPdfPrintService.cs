using System.Diagnostics;
using System.Drawing.Printing;
using local_print_agent.Models;
using Microsoft.Extensions.Options;

namespace local_print_agent.Services;

public class SumatraPdfPrintService : IPdfPrintService
{
    private const int A4Width = 827;
    private const int A4Height = 1169;
    private const int A5Width = 583;
    private const int A5Height = 827;
    private const int PaperSizeTolerance = 10;

    private readonly ILogger<SumatraPdfPrintService> _logger;
    private readonly PrintAgentOptions _options;

    public SumatraPdfPrintService(ILogger<SumatraPdfPrintService> logger, IOptions<PrintAgentOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task<PrintExecutionResult> PrintPdfAsync(PrintRequest request, CancellationToken cancellationToken = default)
    {
        var sumatraPath = _options.Pdf.SumatraPath;
        if (string.IsNullOrWhiteSpace(sumatraPath) || !File.Exists(sumatraPath))
        {
            throw new PrintServiceException("PDF_RENDERER_NOT_FOUND", "SumatraPDF nije pronadjen. Proveri PrintAgent:Pdf:SumatraPath.", StatusCodes.Status500InternalServerError);
        }

        var printerName = PrinterCatalogService.ResolvePrinterOrThrow(request.PrinterName);
        var pdfBytes = DecodePayload(request.DocumentBase64!);

        var tempPdfPath = Path.Combine(Path.GetTempPath(), $"local-print-agent-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(tempPdfPath, pdfBytes, cancellationToken);

        try
        {
            var (printSettings, effectivePaperSize, usedFallback) = BuildPrintSettings(request, printerName);
            var args = $"-silent -print-to \"{printerName}\" -print-settings \"{printSettings}\" \"{tempPdfPath}\"";

            if (usedFallback)
            {
                _logger.LogWarning(
                    "Requested paper size A5 is not supported by printer {Printer}. Falling back to A4.",
                    printerName);
            }

            using var process = new Process();
            process.StartInfo.FileName = sumatraPath;
            process.StartInfo.Arguments = args;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            if (!process.Start())
            {
                throw new PrintServiceException("PRINT_FAILED", "Pokretanje SumatraPDF procesa nije uspelo.", StatusCodes.Status500InternalServerError);
            }

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.PrintTimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                await process.WaitForExitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                TryKillProcess(process);

                if (timeoutCts.IsCancellationRequested)
                {
                    throw new PrintServiceException("PRINT_TIMEOUT", $"PDF stampa je prekoracila timeout od {_options.PrintTimeoutSeconds}s.", StatusCodes.Status504GatewayTimeout);
                }

                throw;
            }

            if (process.ExitCode != 0)
            {
                throw new PrintServiceException("PRINT_FAILED", $"SumatraPDF je zavrsio sa greskom (exit code: {process.ExitCode}).", StatusCodes.Status500InternalServerError);
            }

            _logger.LogInformation(
                "PDF print completed. AppId={AppId} Mode={Mode} Printer={Printer} Copies={Copies}",
                request.AppId,
                request.Mode,
                printerName,
                request.Copies);

            return new PrintExecutionResult
            {
                Mode = "pdf",
                PrinterUsed = printerName,
                PaperSize = effectivePaperSize,
                Copies = request.Copies,
                Message = usedFallback
                    ? "PDF je uspesno poslat na stampu (fallback na A4 jer A5 nije podrzan na printeru)."
                    : "PDF je uspesno poslat na stampu."
            };
        }
        finally
        {
            TryDeleteTempFile(tempPdfPath);
        }
    }

    private static (string PrintSettings, string EffectivePaperSize, bool UsedFallback) BuildPrintSettings(PrintRequest request, string printerName)
    {
        var orientation = request.Orientation!.Equals("landscape", StringComparison.OrdinalIgnoreCase)
            ? "landscape"
            : "portrait";

        var paper = request.PaperSize ?? "A4";
        var supportsA5 = SupportsPaper(printerName, PaperKind.A5, "A5");
        var supportsA4 = SupportsPaper(printerName, PaperKind.A4, "A4");

        if (paper.Equals("A5", StringComparison.OrdinalIgnoreCase))
        {
            if (supportsA5)
            {
                // DMPAPER_A5 = 11.
                return ($"{request.Copies}x,{orientation},paperkind=11", "A5", false);
            }

            if (supportsA4)
            {
                // DMPAPER_A4 = 9.
                return ($"{request.Copies}x,{orientation},paperkind=9", "A4", true);
            }

            throw new PrintServiceException(
                "PRINT_FAILED",
                $"Printer '{printerName}' ne podrzava ni A5 ni A4 format za PDF stampu.",
                StatusCodes.Status500InternalServerError);
        }

        if (supportsA4)
        {
            // DMPAPER_A4 = 9.
            return ($"{request.Copies}x,{orientation},paperkind=9", "A4", false);
        }

        throw new PrintServiceException(
            "PRINT_FAILED",
            $"Printer '{printerName}' ne podrzava A4 format za PDF stampu.",
            StatusCodes.Status500InternalServerError);
    }

    private static bool SupportsPaper(string printerName, PaperKind expectedKind, string expectedName)
    {
        var settings = new PrinterSettings
        {
            PrinterName = printerName
        };

        if (!settings.IsValid)
        {
            throw new PrintServiceException(
                "PRINTER_NOT_FOUND",
                $"Printer '{printerName}' nije dostupan.",
                StatusCodes.Status404NotFound);
        }

        foreach (PaperSize size in settings.PaperSizes)
        {
            if (size.Kind == expectedKind)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(size.PaperName)
                && size.PaperName.Contains(expectedName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (MatchesExpectedSize(size, expectedName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesExpectedSize(PaperSize size, string expectedName)
    {
        if (expectedName.Equals("A4", StringComparison.OrdinalIgnoreCase))
        {
            return MatchesDimensions(size, A4Width, A4Height);
        }

        if (expectedName.Equals("A5", StringComparison.OrdinalIgnoreCase))
        {
            return MatchesDimensions(size, A5Width, A5Height);
        }

        return false;
    }

    private static bool MatchesDimensions(PaperSize size, int width, int height)
    {
        var directMatch = Math.Abs(size.Width - width) <= PaperSizeTolerance
            && Math.Abs(size.Height - height) <= PaperSizeTolerance;

        var rotatedMatch = Math.Abs(size.Width - height) <= PaperSizeTolerance
            && Math.Abs(size.Height - width) <= PaperSizeTolerance;

        return directMatch || rotatedMatch;
    }

    private static byte[] DecodePayload(string base64)
    {
        try
        {
            return Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            throw new PrintServiceException("INVALID_BASE64", "documentBase64 nije validan Base64.", StatusCodes.Status400BadRequest);
        }
    }

    private static void TryDeleteTempFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Temp cleanup failure should not fail the request.
        }
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }
        catch
        {
            // If process already ended, no action is needed.
        }
    }
}
