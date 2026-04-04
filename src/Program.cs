namespace RKLLM;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using RKLLM.Abstractions;
using RKLLM.Configuration;
using RKLLM.Controllers;
using RKLLM.Dtos;
using RKLLM.Infra;
using RKLLM.Logging;

public class Program {
    public static void Main(string[] args) {
        NLogConfigurator.Configure();

        var applicationConfig = AppConfigLoader.Load();
        var builder = WebApplication.CreateSlimBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Logging.AddNLog();
        builder.Logging.AddFilter("Microsoft", LogLevel.Warning);

        builder.WebHost.ConfigureKestrel(options => {
            options.ListenAnyIP(applicationConfig.Rkllm.Port);
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
            applicationConfig.Rkllm.MaxContextLen));
        builder.Services.AddSingleton<IModelInferenceService, RkllmInferenceService>();

        var app = builder.Build();
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        var webRootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
        var indexFilePath = Path.Combine(webRootPath, "index.html");

        logger.LogInformation("RKLLM server configured on port {Port} with model {Model}", applicationConfig.Rkllm.Port, applicationConfig.Rkllm.ModelPath);
        logger.LogInformation("Initializing RKLLM runtime at startup...");
        _ = app.Services.GetRequiredService<RkllmWrapper>();
        logger.LogInformation("RKLLM runtime initialized.");

        if (Directory.Exists(webRootPath)) {
            logger.LogInformation("Serving static UI from {WebRootPath}", webRootPath);
            app.UseDefaultFiles();
            app.UseStaticFiles();
        }

        app.MapGet("/health", (RkllmOptions options) => TypedResults.Ok(new ServiceInfoResponse {
            Service = "RkllmChat",
            Port = options.Port,
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
}
