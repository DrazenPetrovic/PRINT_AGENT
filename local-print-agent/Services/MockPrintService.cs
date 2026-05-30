using local_print_agent.Models;

namespace local_print_agent.Services;

public class MockPrintService : IPrintService
{
    private readonly ILogger<MockPrintService> _logger;

    public MockPrintService(ILogger<MockPrintService> logger)
    {
        _logger = logger;
    }

    public Task<PrintResponse> QueuePrintAsync(PrintRequest request, CancellationToken cancellationToken = default)
    {
        var jobId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;

        _logger.LogInformation(
            "Mock print queued. JobId={JobId}, AppId={AppId}, Type={DocumentType}, Size={PaperSize}, Orientation={Orientation}, Copies={Copies}, Printer={PrinterName}",
            jobId,
            request.AppId,
            request.DocumentType,
            request.PaperSize,
            request.Orientation,
            request.Copies,
            string.IsNullOrWhiteSpace(request.PrinterName) ? "(default)" : request.PrinterName);

        var summary = $"Queued mock print job for app '{request.AppId}' ({request.DocumentType}, {request.PaperSize}, {request.Orientation}, copies: {request.Copies}).";

        return Task.FromResult(new PrintResponse
        {
            Ok = true,
            JobId = jobId.ToString(),
            Timestamp = timestamp,
            Summary = summary
        });
    }
}
