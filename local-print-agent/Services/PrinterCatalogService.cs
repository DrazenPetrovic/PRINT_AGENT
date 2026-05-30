using System.Drawing.Printing;

namespace local_print_agent.Services;

public static class PrinterCatalogService
{
    public static IReadOnlyList<string> GetInstalledPrinters()
    {
        return PrinterSettings.InstalledPrinters.Cast<string>().OrderBy(x => x).ToList();
    }

    public static string? GetDefaultPrinterName()
    {
        var defaultPrinter = new PrinterSettings().PrinterName;
        return string.IsNullOrWhiteSpace(defaultPrinter) ? null : defaultPrinter;
    }

    public static string ResolvePrinterOrThrow(string? requestedPrinter)
    {
        var printers = GetInstalledPrinters();

        if (!string.IsNullOrWhiteSpace(requestedPrinter))
        {
            var match = printers.FirstOrDefault(p => string.Equals(p, requestedPrinter, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }

            throw new PrintServiceException("PRINTER_NOT_FOUND", $"Printer '{requestedPrinter}' nije pronadjen.", StatusCodes.Status404NotFound);
        }

        var defaultPrinter = GetDefaultPrinterName();
        if (defaultPrinter is null)
        {
            throw new PrintServiceException("PRINTER_NOT_FOUND", "Podrazumevani printer nije konfigurisan.", StatusCodes.Status404NotFound);
        }

        return defaultPrinter;
    }
}
