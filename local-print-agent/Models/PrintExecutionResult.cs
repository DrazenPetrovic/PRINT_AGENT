namespace local_print_agent.Models;

public class PrintExecutionResult
{
    public string Mode { get; set; } = string.Empty;
    public string PrinterUsed { get; set; } = string.Empty;
    public string? PaperSize { get; set; }
    public int Copies { get; set; }
    public string Message { get; set; } = string.Empty;
}
