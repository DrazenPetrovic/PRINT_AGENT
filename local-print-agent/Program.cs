using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using local_print_agent.Endpoints;
using local_print_agent.Models;
using local_print_agent.Services;

var webAppOptions = new WebApplicationOptions
{
	Args = args,
	ContentRootPath = AppContext.BaseDirectory
};

var builder = WebApplication.CreateBuilder(webAppOptions);
builder.Host.UseWindowsService();

builder.Services.Configure<PrintAgentOptions>(builder.Configuration.GetSection("PrintAgent"));
var options = builder.Configuration.GetSection("PrintAgent").Get<PrintAgentOptions>() ?? new PrintAgentOptions();

var bindAddress = "127.0.0.1";
var port = options.Port > 0 ? options.Port : 4567;
var allowedOrigins = options.Cors.AllowedOrigins;
var useMockService = options.UseMockService;

builder.WebHost.UseUrls($"http://{bindAddress}:{port}");
if (useMockService)
{
    builder.Services.AddSingleton<IPrintService, MockPrintService>();
}
else
{
	builder.Services.AddSingleton<IPdfPrintService, SumatraPdfPrintService>();
    builder.Services.AddSingleton<IPrintService, WindowsPrintService>();
}

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
	errorApp.Run(async context =>
	{
		var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
		context.Response.StatusCode = StatusCodes.Status500InternalServerError;
		context.Response.ContentType = "application/json";

		await context.Response.WriteAsJsonAsync(new
		{
			code = "internal_error",
			message = "Doslo je do neocekivane greske.",
			details = exception?.Message
		});
	});
});

app.Use(async (context, next) =>
{
	var stopwatch = Stopwatch.StartNew();
	await next();
	stopwatch.Stop();

	app.Logger.LogInformation(
		"Timestamp={Timestamp} Level=Information Method={Method} Path={Path} Status={StatusCode} DurationMs={ElapsedMs}",
		DateTimeOffset.UtcNow,
		context.Request.Method,
		context.Request.Path,
		context.Response.StatusCode,
		stopwatch.ElapsedMilliseconds);
});

app.Use(async (context, next) =>
{
	var origin = context.Request.Headers.Origin.ToString();
	var isOriginAllowed = !string.IsNullOrWhiteSpace(origin)
		&& allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);

	if (isOriginAllowed)
	{
		context.Response.Headers["Access-Control-Allow-Origin"] = origin;
		context.Response.Headers["Access-Control-Allow-Private-Network"] = "true";
		context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
		context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization";
		context.Response.Headers["Vary"] = "Origin";
	}

	if (HttpMethods.IsOptions(context.Request.Method) && isOriginAllowed)
	{
		context.Response.Headers["Access-Control-Max-Age"] = "600";
		context.Response.StatusCode = StatusCodes.Status204NoContent;
		return;
	}

	await next();
});

app.MapPrintEndpoints();

app.Lifetime.ApplicationStarted.Register(() =>
{
	app.Logger.LogInformation(
		"local-print-agent STARTED on http://{BindAddress}:{Port} (mode: {Mode})",
		bindAddress,
		port,
		useMockService ? "mock" : "real");
});

app.Run();
