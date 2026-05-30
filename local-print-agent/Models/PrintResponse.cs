namespace local_print_agent.Models;

public class PrintResponse
{
    public bool Ok { get; set; }
    public string JobId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string Summary { get; set; } = string.Empty;
}
