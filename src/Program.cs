namespace RkllmChat;

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using RkllmChat.Abstractions;
using RkllmChat.Configuration;
using RkllmChat.Controllers;
using RkllmChat.Dtos;
using RkllmChat.Infra.Rkllm;
using RkllmChat.Infra.Rknn;
using RkllmChat.Logging;

public class Program {
    public static void Main(string[] args) {
        var enableDebugLogging = args.Any(arg => string.Equals(arg, "--debug", StringComparison.OrdinalIgnoreCase));
        NLogConfigurator.Configure(enableDebugLogging);

        var applicationConfig = AppConfigLoader.Load(args);
        var builder = WebApplication.CreateSlimBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(enableDebugLogging ? LogLevel.Debug : LogLevel.Information);
        builder.Logging.AddNLog();
        builder.Logging.AddFilter("Microsoft", LogLevel.Warning);

        builder.WebHost.ConfigureKestrel(options => {
            ConfigureKestrel(options, applicationConfig.Rkllm.Server);
        });

        builder.Services.ConfigureHttpJsonOptions(options => {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
        });

        builder.Services.AddSingleton(applicationConfig);
        builder.Services.AddSingleton(applicationConfig.Rkllm);
        builder.Services.AddSingleton<IPromptFormatter, ChatPromptFormatter>();
        builder.Services.AddSingleton<RkllmWrapper>(_ => new RkllmWrapper(
            applicationConfig.Rkllm.ModelPath,
            applicationConfig.Rkllm.Platform,
            applicationConfig.Rkllm.MaxContextLen,
            applicationConfig.Rkllm.Llm));
        builder.Services.AddSingleton<RknnVisionEncoder>(serviceProvider =>
            new RknnVisionEncoder(serviceProvider.GetRequiredService<ILogger<RknnVisionEncoder>>()));
        builder.Services.AddSingleton<IModelInferenceService, ChatInferenceService>();

        var app = builder.Build();
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        var webRootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
        var indexFilePath = Path.Combine(webRootPath, "index.html");

        PrintMotd(logger);

        logger.LogDebug(
            "Configuration loaded. Port={Port}, Platform={Platform}, MaxContextLen={MaxContextLen}, HasVlModel={HasVlModel}",
            applicationConfig.Rkllm.Port,
            applicationConfig.Rkllm.Platform,
            applicationConfig.Rkllm.MaxContextLen,
            applicationConfig.Rkllm.VlModel is not null);
        logger.LogDebug("Resolved web root path: {WebRootPath}, index file: {IndexFilePath}", webRootPath, indexFilePath);

        logger.LogInformation(
            "RKLLM server configured on {Host}:{Port} with model {Model}",
            applicationConfig.Rkllm.Server.Host,
            applicationConfig.Rkllm.Server.Port,
            applicationConfig.Rkllm.ModelPath);
        logger.LogInformation("Initializing RKLLM runtime at startup...");
        _ = app.Services.GetRequiredService<RkllmWrapper>();
        logger.LogInformation("RKLLM runtime initialized.");

        if (applicationConfig.Rkllm.VlModel is { Path.Length: > 0 } vlModel) {
            logger.LogDebug("VL model configuration: Path={VlModelPath}, Width={Width}, Height={Height}, CoreMask={CoreMask}", vlModel.Path, vlModel.Width, vlModel.Height, vlModel.CoreMask);
            logger.LogInformation("Initializing RKNN vision encoder at startup from {VlModelPath} with coreMask = {CoreMask}...", vlModel.Path, vlModel.CoreMask);
            var visionEncoder = app.Services.GetRequiredService<RknnVisionEncoder>();
            if (!visionEncoder.Initialize(vlModel.Path, (uint)vlModel.CoreMask)) {
                throw new InvalidOperationException($"Failed to initialize RKNN vision encoder from '{vlModel.Path}'.");
            }

            logger.LogInformation("RKNN vision encoder initialized with target size {Width}x{Height}.", vlModel.Width, vlModel.Height);
        }
        else {
            logger.LogDebug("No VL model configured; RKNN vision encoder initialization is skipped.");
        }

        if (Directory.Exists(webRootPath)) {
            logger.LogInformation("Serving static UI from {WebRootPath}", webRootPath);
            app.UseDefaultFiles();
            app.UseStaticFiles();
        }

        app.MapGet("/health", (AppOptions options) => TypedResults.Ok(new ServiceInfoResponse {
            Service = "RkllmChat",
            Port = options.Server.Port,
            Model = options.ModelPath
        }));

        app.MapOpenAIEndpoints();
        app.MapOllamaEndpoints();

        if (File.Exists(indexFilePath)) {
            app.MapFallback(async context => {
                if (context.Request.Path.StartsWithSegments("/api") ||
                    context.Request.Path.StartsWithSegments("/v1")) {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.SendFileAsync(indexFilePath);
            });
        }

        logger.LogInformation("RKLLM server is starting.");
        app.Run();
    }

    private static void ConfigureKestrel(KestrelServerOptions options, ServerOptions serverOptions) {
        var host = serverOptions.Host?.Trim();
        var port = serverOptions.Port;

        if (string.IsNullOrWhiteSpace(host) ||
            string.Equals(host, "0.0.0.0", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "*", StringComparison.OrdinalIgnoreCase)) {
            options.ListenAnyIP(port);
            return;
        }

        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) {
            options.ListenLocalhost(port);
            return;
        }

        if (IPAddress.TryParse(host, out var ipAddress)) {
            options.Listen(ipAddress, port);
            return;
        }

        throw new InvalidOperationException($"Invalid server host '{host}'. Use an IP address, localhost, or 0.0.0.0.");
    }

    private static void PrintMotd(ILogger logger) {
        var assembly = typeof(Program).Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("MOTD.txt", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null) {
            logger.LogDebug("Embedded MOTD.txt was not found. Skipping startup banner.");
            return;
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null) {
            logger.LogDebug("Embedded MOTD.txt could not be opened. Skipping startup banner.");
            return;
        }

        using var reader = new StreamReader(stream);
        var motd = reader.ReadToEnd().Trim();
        if (string.IsNullOrWhiteSpace(motd)) {
            logger.LogDebug("Embedded MOTD.txt is empty. Skipping startup banner.");
            return;
        }

        Console.WriteLine(motd);
    }
}
