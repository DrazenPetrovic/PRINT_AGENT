using local_print_agent.Models;
using local_print_agent.Services;

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

        endpoints.MapPost("/print", async (
            PrintRequest request,
            IConfiguration configuration,
            IPrintService printService,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var apiKey = configuration["PrintAgent:ApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                var providedKey = httpContext.Request.Headers["X-Print-Agent-Key"].ToString();
                if (string.IsNullOrWhiteSpace(providedKey) || !string.Equals(providedKey, apiKey, StringComparison.Ordinal))
                {
                    return Results.Json(new
                    {
                        code = "unauthorized",
                        message = "Nedostaje ili je neispravan X-Print-Agent-Key header.",
                        details = (object?)null
                    }, statusCode: StatusCodes.Status401Unauthorized);
                }
            }

            var validationErrors = ValidateRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.Json(new
                {
                    code = "validation_error",
                    message = "Zahtev nije validan.",
                    details = validationErrors
                }, statusCode: StatusCodes.Status400BadRequest);
            }

            var allowedApps = configuration.GetSection("PrintAgent:AllowedApps").Get<string[]>() ?? Array.Empty<string>();
            if (allowedApps.Length > 0 && !allowedApps.Contains(request.AppId, StringComparer.OrdinalIgnoreCase))
            {
                return Results.Json(new
                {
                    code = "forbidden_app",
                    message = "Aplikacija nema dozvolu za koriscenje print agenta.",
                    details = new { appId = request.AppId }
                }, statusCode: StatusCodes.Status403Forbidden);
            }

            var response = await printService.QueuePrintAsync(request, cancellationToken);
            return Results.Ok(response);
        });

        return endpoints;
    }

    private static List<string> ValidateRequest(PrintRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.AppId))
        {
            errors.Add("appId je obavezan.");
        }

        if (string.IsNullOrWhiteSpace(request.DocumentType))
        {
            errors.Add("documentType je obavezan.");
        }

        if (string.IsNullOrWhiteSpace(request.PaperSize) ||
            !(request.PaperSize.Equals("A4", StringComparison.OrdinalIgnoreCase) || request.PaperSize.Equals("A5", StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("paperSize mora biti A4 ili A5.");
        }

        if (string.IsNullOrWhiteSpace(request.Orientation) ||
            !(request.Orientation.Equals("portrait", StringComparison.OrdinalIgnoreCase) || request.Orientation.Equals("landscape", StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("orientation mora biti portrait ili landscape.");
        }

        if (request.Copies < 1 || request.Copies > 20)
        {
            errors.Add("copies mora biti izmedju 1 i 20.");
        }

        if (string.IsNullOrWhiteSpace(request.DocumentBase64))
        {
            errors.Add("documentBase64 je obavezan.");
        }
        else if (!IsValidBase64(request.DocumentBase64))
        {
            errors.Add("documentBase64 nije validan Base64 sadrzaj.");
        }

        return errors;
    }

    private static bool IsValidBase64(string value)
    {
        try
        {
            Convert.FromBase64String(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
