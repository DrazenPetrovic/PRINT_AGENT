using local_print_agent.Models;

namespace local_print_agent.Services;

public class MockPrintService : IPrintService
{
    private readonly ILogger<MockPrintService> _logger;

    public MockPrintService(ILogger<MockPrintService> logger)
    {
        _logger = logger;
    }

    public Task<PrintExecutionResult> QueuePrintAsync(PrintRequest request, CancellationToken cancellationToken = default)
    {
        var printer = string.IsNullOrWhiteSpace(request.PrinterName) ? "(default)" : request.PrinterName;
        var mode = request.Mode ?? "text";

        _logger.LogInformation(
            "Mock print queued. AppId={AppId} Mode={Mode} Type={DocumentType} PaperSize={PaperSize} Orientation={Orientation} Copies={Copies} Printer={Printer}",
            request.AppId,
            mode,
            request.DocumentType,
            request.PaperSize,
            request.Orientation,
            request.Copies,
            printer);

        return Task.FromResult(new PrintExecutionResult
        {
            Mode = mode,
            PrinterUsed = printer,
            PaperSize = request.PaperSize,
            Copies = request.Copies,
            Message = "Mock print uspesno prihvacen."
        });
    }
}
