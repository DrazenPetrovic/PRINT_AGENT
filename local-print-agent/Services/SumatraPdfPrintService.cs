using System.Diagnostics;
using local_print_agent.Models;
using Microsoft.Extensions.Options;

namespace local_print_agent.Services;

public class SumatraPdfPrintService : IPdfPrintService
{
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
            var printSettings = BuildPrintSettings(request);
            var args = $"-silent -print-to \"{printerName}\" -print-settings \"{printSettings}\" \"{tempPdfPath}\"";

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
                PaperSize = request.PaperSize,
                Copies = request.Copies,
                Message = "PDF je uspesno poslat na stampu."
            };
        }
        finally
        {
            TryDeleteTempFile(tempPdfPath);
        }
    }

    private static string BuildPrintSettings(PrintRequest request)
    {
        var orientation = request.Orientation!.Equals("landscape", StringComparison.OrdinalIgnoreCase)
            ? "landscape"
            : "portrait";

        var paper = request.PaperSize ?? "A4";

        // NRG MP 5054 ne prepoznaje "paper=A5" (tiho ga zamijeni A4 ladicom).
        // DMPAPER_A5 = 11 tjera drajver da tačno pogodi Tray 2 (A5 forma).
        var paperToken = paper.Equals("A5", StringComparison.OrdinalIgnoreCase)
            ? "paperkind=11"
            : $"paper={paper}";

        return $"{request.Copies}x,{orientation},{paperToken}";
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
