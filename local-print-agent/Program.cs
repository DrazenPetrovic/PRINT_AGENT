using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using local_print_agent.Endpoints;
using local_print_agent.Models;
using local_print_agent.Services;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddCors(options =>
{
	options.AddPolicy("PrintAgentCors", policy =>
	{
		if (allowedOrigins.Length > 0)
		{
			policy.WithOrigins(allowedOrigins)
				.AllowAnyHeader()
				.AllowAnyMethod();
		}
	});
});

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

app.UseCors("PrintAgentCors");

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
