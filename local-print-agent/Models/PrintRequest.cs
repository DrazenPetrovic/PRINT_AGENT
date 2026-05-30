namespace local_print_agent.Models;

public class PrintRequest
{
    public string? AppId { get; set; }
    public string? DocumentType { get; set; }
    public string? PaperSize { get; set; }
    public string? Orientation { get; set; }
    public string? PrinterName { get; set; }
    public int Copies { get; set; } = 1;
    public string? DocumentBase64 { get; set; }
}
