using local_print_agent.Models;

namespace local_print_agent.Services;

public interface IPdfPrintService
{
    Task<PrintExecutionResult> PrintPdfAsync(PrintRequest request, CancellationToken cancellationToken = default);
}
