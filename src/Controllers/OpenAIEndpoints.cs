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
        return endpoints;
    }

    private static async Task<IResult> StreamChatCompletionAsync(
        HttpContext httpContext,
        ChatCompletionRequest request,
        IModelInferenceService inferenceService,
        AppOptions options,
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

}

