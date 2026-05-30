using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using local_print_agent.Endpoints;
using local_print_agent.Services;

var builder = WebApplication.CreateBuilder(args);

var bindAddress = builder.Configuration["PrintAgent:BindAddress"] ?? "127.0.0.1";
var portValue = builder.Configuration["PrintAgent:Port"];
var port = int.TryParse(portValue, out var parsedPort) ? parsedPort : 4567;
var allowedOrigins = builder.Configuration.GetSection("PrintAgent:Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

builder.WebHost.UseUrls($"http://{bindAddress}:{port}");
builder.Services.AddSingleton<IPrintService, MockPrintService>();
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
		"{Method} {Path} -> {StatusCode} ({ElapsedMs} ms)",
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
		"local-print-agent STARTED on http://{BindAddress}:{Port}",
		bindAddress,
		port);
});

app.Run();
