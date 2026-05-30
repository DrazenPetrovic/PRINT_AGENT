using System.Drawing;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using System.Text;
using local_print_agent.Models;

namespace local_print_agent.Services;

public class WindowsPrintService : IPrintService
{
    private readonly ILogger<WindowsPrintService> _logger;
    private readonly IPdfPrintService _pdfPrintService;

    public WindowsPrintService(ILogger<WindowsPrintService> logger, IPdfPrintService pdfPrintService)
    {
        _logger = logger;
        _pdfPrintService = pdfPrintService;
    }

    public async Task<PrintExecutionResult> QueuePrintAsync(PrintRequest request, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PrintServiceException("PRINT_FAILED", "Realno stampanje je podrzano samo na Windows-u.", StatusCodes.Status500InternalServerError);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var mode = request.Mode!.ToLowerInvariant();

        if (mode == "pdf")
        {
            return await _pdfPrintService.PrintPdfAsync(request, cancellationToken);
        }

        var printerName = PrinterCatalogService.ResolvePrinterOrThrow(request.PrinterName);
        var payload = DecodePayload(request.DocumentBase64!);

        if (mode == "raw")
        {
            SendRawToPrinter(printerName, payload, request.Copies);
        }
        else if (mode == "text")
        {
            var text = Encoding.UTF8.GetString(payload);
            PrintTextDocument(printerName, text, request);
        }
        else
        {
            throw new PrintServiceException("INVALID_REQUEST", "Nepodrzan mode. Dozvoljeno: text, raw, pdf.", StatusCodes.Status400BadRequest);
        }

        _logger.LogInformation(
            "Print completed. AppId={AppId} Mode={Mode} Printer={PrinterName} Copies={Copies}",
            request.AppId,
            mode,
            printerName,
            request.Copies);

        return new PrintExecutionResult
        {
            Mode = mode,
            PrinterUsed = printerName,
            PaperSize = request.PaperSize,
            Copies = request.Copies,
            Message = "Dokument je uspesno poslat na stampu."
        };
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

    private static void PrintTextDocument(string printerName, string text, PrintRequest request)
    {
        var orientation = request.Orientation ?? "portrait";
        var paperSize = request.PaperSize ?? "A4";

        using var printDoc = new PrintDocument();
        printDoc.PrinterSettings.PrinterName = printerName;
        printDoc.PrinterSettings.Copies = (short)request.Copies;
        printDoc.DefaultPageSettings.Landscape = orientation.Equals("landscape", StringComparison.OrdinalIgnoreCase);
        printDoc.DefaultPageSettings.PaperSize = ResolvePaperSize(paperSize);

        printDoc.PrintPage += (_, args) =>
        {
            using var font = new Font("Segoe UI", 10f);
            var bounds = args.MarginBounds;
            if (args.Graphics is null)
            {
                throw new PrintServiceException("printer_graphics_missing", "Printer nije vratio validan graficki kontekst.", StatusCodes.Status500InternalServerError);
            }

            args.Graphics.DrawString(text, font, Brushes.Black, bounds);
            args.HasMorePages = false;
        };

        try
        {
            printDoc.Print();
        }
        catch (Exception ex)
        {
            throw new PrintServiceException("PRINT_FAILED", $"Stampanje nije uspelo: {ex.Message}", StatusCodes.Status500InternalServerError);
        }
    }

    private static PaperSize ResolvePaperSize(string paperSize)
    {
        return paperSize.Equals("A5", StringComparison.OrdinalIgnoreCase)
            ? new PaperSize("A5", 583, 827)
            : new PaperSize("A4", 827, 1169);
    }

    private static void SendRawToPrinter(string printerName, byte[] data, int copies)
    {
        for (var i = 0; i < copies; i++)
        {
            RawPrinterHelper.SendRaw(printerName, data, "local-print-agent raw job");
        }
    }

    private static class RawPrinterHelper
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private class DocInfo
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string? pDocName;

            [MarshalAs(UnmanagedType.LPWStr)]
            public string? pOutputFile;

            [MarshalAs(UnmanagedType.LPWStr)]
            public string? pDataType;
        }

        [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

        [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In] DocInfo pDocInfo);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

        public static void SendRaw(string printerName, byte[] bytes, string jobName)
        {
            if (!OpenPrinter(printerName, out var printerHandle, IntPtr.Zero))
            {
                throw new PrintServiceException("PRINT_FAILED", $"Nije moguce otvoriti printer '{printerName}'.", StatusCodes.Status500InternalServerError);
            }

            try
            {
                var docInfo = new DocInfo
                {
                    pDocName = jobName,
                    pDataType = "RAW"
                };

                if (!StartDocPrinter(printerHandle, 1, docInfo))
                {
                    throw new PrintServiceException("PRINT_FAILED", "Nije moguce zapoceti spool posao.", StatusCodes.Status500InternalServerError);
                }

                try
                {
                    if (!StartPagePrinter(printerHandle))
                    {
                        throw new PrintServiceException("PRINT_FAILED", "Nije moguce otvoriti stranicu za stampu.", StatusCodes.Status500InternalServerError);
                    }

                    var unmanagedPointer = Marshal.AllocCoTaskMem(bytes.Length);
                    try
                    {
                        Marshal.Copy(bytes, 0, unmanagedPointer, bytes.Length);
                        if (!WritePrinter(printerHandle, unmanagedPointer, bytes.Length, out _))
                        {
                            throw new PrintServiceException("PRINT_FAILED", "Upis podataka u spool nije uspeo.", StatusCodes.Status500InternalServerError);
                        }
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(unmanagedPointer);
                        EndPagePrinter(printerHandle);
                    }
                }
                finally
                {
                    EndDocPrinter(printerHandle);
                }
            }
            finally
            {
                ClosePrinter(printerHandle);
            }
        }
    }
}