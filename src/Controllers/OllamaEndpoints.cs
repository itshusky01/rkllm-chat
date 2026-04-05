using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RkllmChat.Abstractions;
using RkllmChat.Configuration;
using RkllmChat.Dtos;
using RkllmChat.Models;

namespace RkllmChat.Controllers;

public static class OllamaEndpoints {
    public static IEndpointRouteBuilder MapOllamaEndpoints(this IEndpointRouteBuilder endpoints) {
        endpoints.MapGet("/api/version", GetVersion);
        endpoints.MapGet("/api/tags", GetTags);
        endpoints.MapGet("/api/ps", GetRunningModels);
        endpoints.MapPost("/api/show", ShowModel);
        endpoints.MapPost("/api/generate", GenerateAsync);
        endpoints.MapPost("/api/chat", ChatAsync);
        return endpoints;
    }

    private static IResult GetVersion() => TypedResults.Ok(new OllamaVersionResponse());

    private static IResult GetTags(AppOptions options) {
        return TypedResults.Ok(new OllamaTagsResponse {
            Models = [BuildModelSummary(options)]
        });
    }

    private static IResult GetRunningModels(AppOptions options) {
        var model = BuildModelSummary(options);
        return TypedResults.Ok(new OllamaPsResponse {
            Models = [new OllamaRunningModel {
                Name = model.Name,
                Model = model.Model,
                ModifiedAt = model.ModifiedAt,
                Size = model.Size,
                Digest = model.Digest,
                Details = model.Details,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5).ToString("O"),
                SizeVram = model.Size
            }]
        });
    }

    private static IResult ShowModel(AppOptions options) {
        return TypedResults.Ok(new OllamaShowResponse {
            License = "See the model distribution license.",
            Modelfile = $"FROM {options.ModelPath}\nPARAMETER num_ctx {options.MaxContextLen}",
            Parameters = $"num_ctx {options.MaxContextLen}",
            Template = "<|im_start|>{{ .Role }}\\n{{ .Content }}\\n<|im_end|><|im_start|>assistant\\n",
            Details = BuildModelDetails(),
            ModelInfo = new Dictionary<string, string> {
                ["rkllm.model_path"] = options.ModelPath,
                ["rkllm.platform"] = options.Platform,
                ["rkllm.max_context_len"] = options.MaxContextLen.ToString(),
                ["general.architecture"] = "rkllm"
            }
        });
    }

    private static async Task<IResult> GenerateAsync(
        HttpContext httpContext,
        OllamaGenerateRequest request,
        IModelInferenceService inferenceService,
        AppOptions options,
        CancellationToken cancellationToken) {
        var createdAt = DateTimeOffset.UtcNow;
        var modelName = ResolveModelName(request.Model, options);

        if (string.IsNullOrWhiteSpace(request.Prompt)) {
            return TypedResults.Ok(CreateEmptyGenerateResponse(modelName, createdAt, request.KeepAlive));
        }

        var messages = BuildGenerateMessages(request);
        if (!inferenceService.TryStartChat(messages, request.Think ?? true, cancellationToken, out var responseStream) || responseStream is null) {
            return TypedResults.Text("Server busy", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (request.Stream == false) {
            var generation = await CollectGenerationAsync(responseStream, cancellationToken);
            return TypedResults.Ok(CreateGenerateResponse(modelName, createdAt, generation.Text, generation.Duration, generation.TokenCount));
        }

        httpContext.Response.ContentType = "application/x-ndjson";

        try {
            var stopwatch = Stopwatch.StartNew();
            var tokenCount = 0;

            await foreach (var chunk in responseStream.WithCancellation(cancellationToken)) {
                tokenCount += EstimateTokenCount(chunk);
                var payload = new OllamaGenerateResponse {
                    Model = modelName,
                    CreatedAt = createdAt.ToString("O"),
                    Response = chunk,
                    Done = false
                };

                await JsonSerializer.SerializeAsync(
                    httpContext.Response.Body,
                    payload,
                    AppJsonSerializerContext.Default.OllamaGenerateResponse,
                    cancellationToken);
                await httpContext.Response.WriteAsync("\n", cancellationToken);
                await httpContext.Response.Body.FlushAsync(cancellationToken);
            }

            var finalPayload = CreateGenerateResponse(modelName, createdAt, string.Empty, stopwatch.Elapsed, tokenCount);
            await JsonSerializer.SerializeAsync(
                httpContext.Response.Body,
                finalPayload,
                AppJsonSerializerContext.Default.OllamaGenerateResponse,
                cancellationToken);
            await httpContext.Response.WriteAsync("\n", cancellationToken);
            await httpContext.Response.Body.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
        }

        return Results.Empty;
    }

    private static async Task<IResult> ChatAsync(
        HttpContext httpContext,
        OllamaChatRequest request,
        IModelInferenceService inferenceService,
        AppOptions options,
        CancellationToken cancellationToken) {
        var createdAt = DateTimeOffset.UtcNow;
        var modelName = ResolveModelName(request.Model, options);

        if (request.Messages.Length == 0) {
            return TypedResults.Ok(CreateEmptyChatResponse(modelName, createdAt, request.KeepAlive));
        }

        if (!inferenceService.TryStartChat(request.Messages, request.Think ?? true, cancellationToken, out var responseStream) || responseStream is null) {
            return TypedResults.Text("Server busy", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (request.Stream == false) {
            var generation = await CollectGenerationAsync(responseStream, cancellationToken);
            return TypedResults.Ok(CreateChatResponse(modelName, createdAt, generation.Text, generation.Duration, generation.TokenCount));
        }

        httpContext.Response.ContentType = "application/x-ndjson";

        try {
            var stopwatch = Stopwatch.StartNew();
            var tokenCount = 0;

            await foreach (var chunk in responseStream.WithCancellation(cancellationToken)) {
                tokenCount += EstimateTokenCount(chunk);
                var payload = new OllamaChatResponse {
                    Model = modelName,
                    CreatedAt = createdAt.ToString("O"),
                    Message = new OllamaChatMessage {
                        Role = "assistant",
                        Content = chunk
                    },
                    Done = false
                };

                await JsonSerializer.SerializeAsync(
                    httpContext.Response.Body,
                    payload,
                    AppJsonSerializerContext.Default.OllamaChatResponse,
                    cancellationToken);
                await httpContext.Response.WriteAsync("\n", cancellationToken);
                await httpContext.Response.Body.FlushAsync(cancellationToken);
            }

            var finalPayload = CreateChatResponse(modelName, createdAt, string.Empty, stopwatch.Elapsed, tokenCount);
            await JsonSerializer.SerializeAsync(
                httpContext.Response.Body,
                finalPayload,
                AppJsonSerializerContext.Default.OllamaChatResponse,
                cancellationToken);
            await httpContext.Response.WriteAsync("\n", cancellationToken);
            await httpContext.Response.Body.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
        }

        return Results.Empty;
    }

    private static Message[] BuildGenerateMessages(OllamaGenerateRequest request) {
        var messages = new List<Message>();

        if (!string.IsNullOrWhiteSpace(request.System)) {
            messages.Add(new Message {
                Role = "system",
                Content = request.System
            });
        }

        messages.Add(new Message {
            Role = "user",
            Content = request.Prompt
        });

        return [.. messages];
    }

    private static async Task<(string Text, TimeSpan Duration, int TokenCount)> CollectGenerationAsync(
        IAsyncEnumerable<string> responseStream,
        CancellationToken cancellationToken) {
        var stopwatch = Stopwatch.StartNew();
        var builder = new StringBuilder();
        var tokenCount = 0;

        await foreach (var chunk in responseStream.WithCancellation(cancellationToken)) {
            builder.Append(chunk);
            tokenCount += EstimateTokenCount(chunk);
        }

        return (builder.ToString(), stopwatch.Elapsed, tokenCount);
    }

    private static OllamaGenerateResponse CreateGenerateResponse(string modelName, DateTimeOffset createdAt, string response, TimeSpan duration, int tokenCount) {
        var totalDuration = ToNanoseconds(duration);
        return new OllamaGenerateResponse {
            Model = modelName,
            CreatedAt = createdAt.ToString("O"),
            Response = response,
            Done = true,
            DoneReason = "stop",
            TotalDuration = totalDuration,
            LoadDuration = 0,
            PromptEvalCount = 0,
            PromptEvalDuration = 0,
            EvalCount = tokenCount,
            EvalDuration = totalDuration
        };
    }

    private static OllamaGenerateResponse CreateEmptyGenerateResponse(string modelName, DateTimeOffset createdAt, JsonElement? keepAlive) {
        return new OllamaGenerateResponse {
            Model = modelName,
            CreatedAt = createdAt.ToString("O"),
            Response = string.Empty,
            Done = true,
            DoneReason = IsUnloadRequested(keepAlive) ? "unload" : "load"
        };
    }

    private static OllamaChatResponse CreateChatResponse(string modelName, DateTimeOffset createdAt, string response, TimeSpan duration, int tokenCount) {
        var totalDuration = ToNanoseconds(duration);
        return new OllamaChatResponse {
            Model = modelName,
            CreatedAt = createdAt.ToString("O"),
            Message = new OllamaChatMessage {
                Role = "assistant",
                Content = response
            },
            Done = true,
            DoneReason = "stop",
            TotalDuration = totalDuration,
            LoadDuration = 0,
            PromptEvalCount = 0,
            PromptEvalDuration = 0,
            EvalCount = tokenCount,
            EvalDuration = totalDuration
        };
    }

    private static OllamaChatResponse CreateEmptyChatResponse(string modelName, DateTimeOffset createdAt, JsonElement? keepAlive) {
        return new OllamaChatResponse {
            Model = modelName,
            CreatedAt = createdAt.ToString("O"),
            Message = new OllamaChatMessage {
                Role = "assistant",
                Content = string.Empty
            },
            Done = true,
            DoneReason = IsUnloadRequested(keepAlive) ? "unload" : "load"
        };
    }

    private static async Task WriteGenerateResponseAsync(
        HttpContext httpContext,
        OllamaGenerateResponse response,
        CancellationToken cancellationToken) {
        await JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            response,
            AppJsonSerializerContext.Default.OllamaGenerateResponse,
            cancellationToken);
        await httpContext.Response.WriteAsync("\n", cancellationToken);
        await httpContext.Response.Body.FlushAsync(cancellationToken);
    }

    private static async Task WriteChatResponseAsync(
        HttpContext httpContext,
        OllamaChatResponse response,
        CancellationToken cancellationToken) {
        await JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            response,
            AppJsonSerializerContext.Default.OllamaChatResponse,
            cancellationToken);
        await httpContext.Response.WriteAsync("\n", cancellationToken);
        await httpContext.Response.Body.FlushAsync(cancellationToken);
    }

    private static string ResolveModelName(string? requestedModel, AppOptions options) {
        if (!string.IsNullOrWhiteSpace(requestedModel)) {
            return requestedModel;
        }

        var fileName = Path.GetFileNameWithoutExtension(options.ModelPath);
        return string.IsNullOrWhiteSpace(fileName) ? "rkllm:latest" : $"{fileName}:latest";
    }

    private static OllamaModelSummary BuildModelSummary(AppOptions options) {
        var modelPath = options.ModelPath;
        var fileInfo = File.Exists(modelPath) ? new FileInfo(modelPath) : null;
        var digestBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{modelPath}:{options.Platform}:{options.MaxContextLen}"));

        return new OllamaModelSummary {
            Name = ResolveModelName(null, options),
            Model = ResolveModelName(null, options),
            ModifiedAt = (fileInfo?.LastWriteTimeUtc ?? DateTime.UtcNow).ToString("O"),
            Size = fileInfo?.Length ?? 0,
            Digest = Convert.ToHexStringLower(digestBytes),
            Details = BuildModelDetails()
        };
    }

    private static OllamaModelDetails BuildModelDetails() {
        return new OllamaModelDetails {
            Format = "rkllm",
            Family = "rkllm",
            Families = ["rkllm"],
            ParameterSize = "unknown",
            QuantizationLevel = "unknown"
        };
    }

    private static bool IsUnloadRequested(JsonElement? keepAlive) {
        if (!keepAlive.HasValue) {
            return false;
        }

        var value = keepAlive.Value;
        return value.ValueKind switch {
            JsonValueKind.Number => value.TryGetInt32(out var number) && number == 0,
            JsonValueKind.String => string.Equals(value.GetString(), "0", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static int EstimateTokenCount(string text) {
        return string.IsNullOrWhiteSpace(text)
            ? 0
            : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static long ToNanoseconds(TimeSpan duration) => duration.Ticks * 100;
}
