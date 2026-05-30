using local_print_agent.Models;

namespace local_print_agent.Services;

public interface IPrintService
{
    Task<PrintExecutionResult> QueuePrintAsync(PrintRequest request, CancellationToken cancellationToken = default);
}
