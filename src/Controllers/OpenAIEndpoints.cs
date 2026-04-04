using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RkllmChat.Abstractions;
using RkllmChat.Configuration;
using RkllmChat.Dtos;

namespace RkllmChat.Controllers;

public static class OpenAIEndpoints {
    public static IEndpointRouteBuilder MapOpenAIEndpoints(this IEndpointRouteBuilder endpoints) {
        endpoints.MapPost("/v1/chat/completions", StreamChatCompletionAsync);
        // TODO: Re-enable `/v1/embeddings`
        return endpoints;
    }

    private static async Task<IResult> StreamChatCompletionAsync(
        HttpContext httpContext,
        ChatCompletionRequest request,
        IModelInferenceService inferenceService,
        RkllmOptions options,
        CancellationToken cancellationToken) {
        httpContext.Response.ContentType = "text/event-stream";
        var createdTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (!inferenceService.TryStartChat(request.Messages, request.Think ?? true, cancellationToken, out var responseStream) || responseStream is null) {
            return TypedResults.Text("Server busy", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        try {
            await foreach (var message in responseStream.WithCancellation(cancellationToken)) {
                await WriteStreamChunkAsync(httpContext, createdTime, options.ModelPath, message, cancellationToken);
            }

            await httpContext.Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
            await httpContext.Response.Body.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
        }

        return Results.Empty;
    }

    private static async Task WriteStreamChunkAsync(
        HttpContext httpContext,
        long createdTime,
        string model,
        string content,
        CancellationToken cancellationToken) {
        var payload = new ChatCompletionChunkResponse {
            Id = $"chatcmpl-{createdTime}",
            Object = "chat.completion.chunk",
            Created = createdTime,
            Model = model,
            Choices = [
                new ChatChoice {
                    Index = 0,
                    Delta = new ChatDelta {
                        Content = content
                    }
                }
            ]
        };

        await httpContext.Response.WriteAsync("data: ", cancellationToken);
        await JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            payload,
            AppJsonSerializerContext.Default.ChatCompletionChunkResponse,
            cancellationToken);
        await httpContext.Response.WriteAsync("\n\n", cancellationToken);
        await httpContext.Response.Body.FlushAsync(cancellationToken);
    }

    private static async Task<IResult> CreateEmbeddingsAsync(
        EmbeddingRequest request,
        IModelInferenceService inferenceService,
        RkllmOptions options,
        CancellationToken cancellationToken) {
        if (!TryParseEmbeddingInput(request.Input, out var inputs)) {
            return TypedResults.Text("`input` must be a string or an array of strings.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (inputs.Length == 0) {
            return TypedResults.Text("`input` must not be empty.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!inferenceService.TryCreateEmbeddings(inputs, cancellationToken, out var embeddingsTask) || embeddingsTask is null) {
            return TypedResults.Text("Server busy", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        try {
            var embeddings = await embeddingsTask.WaitAsync(cancellationToken);
            var data = new EmbeddingData[embeddings.Length];
            for (var i = 0; i < embeddings.Length; i++) {
                data[i] = new EmbeddingData {
                    Index = i,
                    Embedding = embeddings[i]
                };
            }

            return TypedResults.Ok(new EmbeddingsResponse {
                Object = "list",
                Data = data,
                Model = string.IsNullOrWhiteSpace(request.Model) ? options.ModelPath : request.Model,
                Usage = new EmbeddingsUsage {
                    PromptTokens = 0,
                    TotalTokens = 0
                }
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            return Results.Empty;
        }
        catch (TimeoutException) {
            return TypedResults.Text("Embedding request timed out.", statusCode: StatusCodes.Status504GatewayTimeout);
        }
    }

    private static bool TryParseEmbeddingInput(JsonElement input, out string[] values) {
        if (input.ValueKind == JsonValueKind.String) {
            values = [input.GetString() ?? string.Empty];
            return true;
        }

        if (input.ValueKind != JsonValueKind.Array) {
            values = [];
            return false;
        }

        values = new string[input.GetArrayLength()];
        var index = 0;
        foreach (var item in input.EnumerateArray()) {
            if (item.ValueKind != JsonValueKind.String) {
                values = [];
                return false;
            }

            values[index++] = item.GetString() ?? string.Empty;
        }

        return true;
    }
}
