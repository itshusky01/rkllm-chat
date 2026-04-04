namespace RKLLM;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RKLLM.Abstractions;
using RKLLM.Configuration;
using RKLLM.Contracts;
using RKLLM.Controllers;
using RKLLM.Infra;

public class Program {
    public static void Main(string[] args) {
        var applicationConfig = AppConfigLoader.Load();
        var builder = WebApplication.CreateSlimBuilder(args);

        builder.WebHost.ConfigureKestrel(options => {
            options.ListenAnyIP(applicationConfig.Rkllm.Port);
        });

        builder.Services.ConfigureHttpJsonOptions(options => {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
        });

        builder.Services.AddSingleton(applicationConfig);
        builder.Services.AddSingleton(applicationConfig.Rkllm);
        builder.Services.AddSingleton<IPromptFormatter, OpenAIPromptFormatter>();
        builder.Services.AddSingleton(_ => new RKLLMWrapper(
            applicationConfig.Rkllm.ModelPath,
            applicationConfig.Rkllm.Platform,
            applicationConfig.Rkllm.MaxContextLen));
        builder.Services.AddSingleton<IModelInferenceService, RkllmInferenceService>();

        var app = builder.Build();

        app.MapGet("/", (RkllmOptions options) => TypedResults.Ok(new ServiceInfoResponse {
            Service = "RKLLM",
            Port = options.Port,
            Model = options.ModelPath
        }));

        app.MapOpenAIEndpoints();
        app.Run();
    }
}
