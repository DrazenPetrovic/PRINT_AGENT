namespace local_print_agent.Models;

public class PrintAgentOptions
{
    public string BindAddress { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 4567;
    public bool UseMockService { get; set; } = true;
    public string? ApiKey { get; set; }
    public string[] AllowedApps { get; set; } = Array.Empty<string>();
    public int MaxPayloadMb { get; set; } = 20;
    public int PrintTimeoutSeconds { get; set; } = 60;
    public PdfOptions Pdf { get; set; } = new();
    public CorsOptions Cors { get; set; } = new();

    public class PdfOptions
    {
        public string SumatraPath { get; set; } = @"C:\Program Files\SumatraPDF\SumatraPDF.exe";
    }

    public class CorsOptions
    {
        public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
    }
}
