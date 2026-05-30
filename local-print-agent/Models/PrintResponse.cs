namespace local_print_agent.Models;

public class PrintResponse
{
    public bool Success { get; set; }
    public string JobId { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string PrinterUsed { get; set; } = string.Empty;
    public string? PaperSize { get; set; }
    public int Copies { get; set; }
    public long DurationMs { get; set; }
    public string? ErrorCode { get; set; }
    public string Message { get; set; } = string.Empty;
}
