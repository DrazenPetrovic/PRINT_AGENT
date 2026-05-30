using local_print_agent.Models;
using local_print_agent.Services;
using Microsoft.Extensions.Options;

namespace local_print_agent.Endpoints;

public static class PrintEndpoints
{
    private const string ServiceName = "local-print-agent";
    private const string Version = "1.0.0";

    public static IEndpointRouteBuilder MapPrintEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", () => Results.Ok(new
        {
            ok = true,
            service = ServiceName,
            version = Version
        }));

        endpoints.MapGet("/status", (IOptions<PrintAgentOptions> optionsAccessor) =>
        {
            var options = optionsAccessor.Value;
            var sumatraPath = options.Pdf.SumatraPath;
            var sumatraExists = !string.IsNullOrWhiteSpace(sumatraPath) && File.Exists(sumatraPath);
            var defaultPrinter = PrinterCatalogService.GetDefaultPrinterName();
            var defaultPrinterExists = !string.IsNullOrWhiteSpace(defaultPrinter);
            var printers = PrinterCatalogService.GetInstalledPrinters();

            return Results.Ok(new
            {
                success = true,
                serviceActive = true,
                pdfRendererActive = sumatraExists,
                sumatraExists,
                sumatraPath,
                defaultPrinterExists,
                defaultPrinter,
                printerCount = printers.Count,
                printers,
                lastCheckTime = DateTimeOffset.UtcNow,
                service = ServiceName,
                version = Version
            });
        });

        endpoints.MapGet("/printers", () =>
        {
            var printers = PrinterCatalogService.GetInstalledPrinters();
            var defaultPrinter = PrinterCatalogService.GetDefaultPrinterName();

            return Results.Ok(new
            {
                success = true,
                printers,
                defaultPrinter
            });
        });

        endpoints.MapGet("/config-check", (IOptions<PrintAgentOptions> optionsAccessor) =>
        {
            var options = optionsAccessor.Value;
            var sumatraPath = options.Pdf.SumatraPath;
            var defaultPrinter = PrinterCatalogService.GetDefaultPrinterName();

            return Results.Ok(new
            {
                success = true,
                sumatraPath,
                sumatraExists = !string.IsNullOrWhiteSpace(sumatraPath) && File.Exists(sumatraPath),
                defaultPrinter,
                defaultPrinterExists = !string.IsNullOrWhiteSpace(defaultPrinter)
            });
        });

        endpoints.MapPost("/print", async (
            PrintRequest request,
            IOptions<PrintAgentOptions> optionsAccessor,
            IPrintService printService,
            ILoggerFactory loggerFactory,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var options = optionsAccessor.Value;
            var logger = loggerFactory.CreateLogger("PrintJob");
            var jobId = Guid.NewGuid().ToString();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var apiKey = options.ApiKey;
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                var providedKey = httpContext.Request.Headers["X-Print-Agent-Key"].ToString();
                if (string.IsNullOrWhiteSpace(providedKey) || !string.Equals(providedKey, apiKey, StringComparison.Ordinal))
                {
                    stopwatch.Stop();
                    return Results.Json(new PrintResponse
                    {
                        Success = false,
                        JobId = jobId,
                        Mode = request.Mode ?? string.Empty,
                        PrinterUsed = string.Empty,
                        PaperSize = request.PaperSize,
                        Copies = request.Copies,
                        DurationMs = stopwatch.ElapsedMilliseconds,
                        ErrorCode = "UNAUTHORIZED",
                        Message = "Nedostaje ili je neispravan X-Print-Agent-Key header."
                    }, statusCode: StatusCodes.Status401Unauthorized);
                }
            }

            request.Orientation = string.IsNullOrWhiteSpace(request.Orientation) ? "portrait" : request.Orientation;
            var validationFailure = ValidateRequest(request, options.MaxPayloadMb);
            if (validationFailure is not null)
            {
                stopwatch.Stop();
                return Results.Json(new PrintResponse
                {
                    Success = false,
                    JobId = jobId,
                    Mode = request.Mode ?? string.Empty,
                    PrinterUsed = string.Empty,
                    PaperSize = request.PaperSize,
                    Copies = request.Copies,
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    ErrorCode = validationFailure.ErrorCode,
                    Message = validationFailure.Message
                }, statusCode: StatusCodes.Status400BadRequest);
            }

            var allowedApps = options.AllowedApps ?? Array.Empty<string>();
            if (allowedApps.Length > 0 && !allowedApps.Contains(request.AppId, StringComparer.OrdinalIgnoreCase))
            {
                stopwatch.Stop();
                return Results.Json(new PrintResponse
                {
                    Success = false,
                    JobId = jobId,
                    Mode = request.Mode ?? string.Empty,
                    PrinterUsed = string.Empty,
                    PaperSize = request.PaperSize,
                    Copies = request.Copies,
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    ErrorCode = "FORBIDDEN_APP",
                    Message = "Aplikacija nema dozvolu za koriscenje print agenta."
                }, statusCode: StatusCodes.Status403Forbidden);
            }

            logger.LogInformation(
                "Timestamp={Timestamp} Level=Information AppId={AppId} Mode={Mode} JobId={JobId} Status={Status}",
                DateTimeOffset.UtcNow,
                request.AppId,
                request.Mode,
                jobId,
                "started");

            try
            {
                var result = await printService.QueuePrintAsync(request, cancellationToken);
                stopwatch.Stop();

                var response = new PrintResponse
                {
                    Success = true,
                    JobId = jobId,
                    Mode = result.Mode,
                    PrinterUsed = result.PrinterUsed,
                    PaperSize = result.PaperSize,
                    Copies = result.Copies,
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    ErrorCode = null,
                    Message = result.Message
                };

                logger.LogInformation(
                    "Timestamp={Timestamp} Level=Information AppId={AppId} Mode={Mode} JobId={JobId} Status={Status}",
                    DateTimeOffset.UtcNow,
                    request.AppId,
                    request.Mode,
                    jobId,
                    "success");

                return Results.Ok(response);
            }
            catch (PrintServiceException ex)
            {
                stopwatch.Stop();

                logger.LogWarning(
                    "Timestamp={Timestamp} Level=Warning AppId={AppId} Mode={Mode} JobId={JobId} Status={Status} ErrorCode={ErrorCode}",
                    DateTimeOffset.UtcNow,
                    request.AppId,
                    request.Mode,
                    jobId,
                    "failed",
                    ex.ErrorCode);

                return Results.Json(new PrintResponse
                {
                    Success = false,
                    JobId = jobId,
                    Mode = request.Mode ?? string.Empty,
                    PrinterUsed = string.Empty,
                    PaperSize = request.PaperSize,
                    Copies = request.Copies,
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    ErrorCode = ex.ErrorCode,
                    Message = ex.Message
                }, statusCode: ex.StatusCode);
            }
        });

        return endpoints;
    }

    private sealed class ValidationFailure
    {
        public string ErrorCode { get; init; } = "INVALID_REQUEST";
        public string Message { get; init; } = "Zahtev nije validan.";
    }

    private static ValidationFailure? ValidateRequest(PrintRequest request, int maxPayloadMb)
    {
        if (string.IsNullOrWhiteSpace(request.AppId))
        {
            return new ValidationFailure { ErrorCode = "INVALID_REQUEST", Message = "appId je obavezan." };
        }

        if (string.IsNullOrWhiteSpace(request.Mode))
        {
            return new ValidationFailure { ErrorCode = "INVALID_REQUEST", Message = "mode je obavezan (text|raw|pdf)." };
        }
        else if (!request.Mode.Equals("text", StringComparison.OrdinalIgnoreCase)
                 && !request.Mode.Equals("raw", StringComparison.OrdinalIgnoreCase)
                 && !request.Mode.Equals("pdf", StringComparison.OrdinalIgnoreCase))
        {
            return new ValidationFailure { ErrorCode = "INVALID_REQUEST", Message = "mode mora biti text, raw ili pdf." };
        }

        if (!string.IsNullOrWhiteSpace(request.PaperSize)
            && !(request.PaperSize.Equals("A4", StringComparison.OrdinalIgnoreCase) || request.PaperSize.Equals("A5", StringComparison.OrdinalIgnoreCase)))
        {
            return new ValidationFailure { ErrorCode = "INVALID_REQUEST", Message = "paperSize mora biti A4 ili A5." };
        }

        if (request.Mode?.Equals("pdf", StringComparison.OrdinalIgnoreCase) == true && string.IsNullOrWhiteSpace(request.PaperSize))
        {
            return new ValidationFailure { ErrorCode = "INVALID_REQUEST", Message = "paperSize je obavezan kada je mode=pdf." };
        }

        if (!(request.Orientation!.Equals("portrait", StringComparison.OrdinalIgnoreCase) || request.Orientation.Equals("landscape", StringComparison.OrdinalIgnoreCase)))
        {
            return new ValidationFailure { ErrorCode = "INVALID_REQUEST", Message = "orientation mora biti portrait ili landscape." };
        }

        if (request.Copies < 1 || request.Copies > 20)
        {
            return new ValidationFailure { ErrorCode = "INVALID_REQUEST", Message = "copies mora biti izmedju 1 i 20." };
        }

        if (string.IsNullOrWhiteSpace(request.DocumentBase64))
        {
            return new ValidationFailure { ErrorCode = "INVALID_REQUEST", Message = "documentBase64 je obavezan." };
        }
        else if (!IsValidBase64(request.DocumentBase64, out var decodedLengthBytes))
        {
            return new ValidationFailure { ErrorCode = "INVALID_BASE64", Message = "documentBase64 nije validan Base64 sadrzaj." };
        }
        else
        {
            var maxBytes = Math.Max(1, maxPayloadMb) * 1024L * 1024L;
            if (decodedLengthBytes > maxBytes)
            {
                return new ValidationFailure { ErrorCode = "PAYLOAD_TOO_LARGE", Message = $"Maksimalna velicina payload-a je {maxPayloadMb} MB." };
            }
        }

        return null;
    }

    private static bool IsValidBase64(string value, out long decodedLengthBytes)
    {
        decodedLengthBytes = 0;

        try
        {
            var buffer = Convert.FromBase64String(value);
            decodedLengthBytes = buffer.LongLength;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
