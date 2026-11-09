using System.Text.Json;
using EvTicket.Api.Common;
using Microsoft.EntityFrameworkCore;

namespace EvTicket.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment environment)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApiException ex)
        {
            await WriteAsync(context, ex.StatusCode, ex.Message, ex.Code);
        }
        catch (BadHttpRequestException ex)
        {
            await WriteAsync(context, StatusCodes.Status400BadRequest, ex.Message, "bad_request");
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // client disconnected; nothing to write
        }
        catch (DbUpdateConcurrencyException ex)
        {
            foreach (var entry in ex.Entries)
            {
                logger.LogWarning(
                    "Concurrency conflict on {Entity} ({State}); keys: {Keys}",
                    entry.Metadata.ClrType.Name,
                    entry.State,
                    string.Join(", ", entry.Properties
                        .Where(p => p.Metadata.IsKey())
                        .Select(p => $"{p.Metadata.Name}={p.CurrentValue}")));
            }

            await WriteAsync(context, StatusCodes.Status409Conflict, "The record changed while you were working on it. Reload and try again.", "concurrency_conflict");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            var message = environment.IsDevelopment() ? ex.Message : "An unexpected error occurred. Please try again.";
            await WriteAsync(context, StatusCodes.Status500InternalServerError, message, "server_error");
        }
    }

    private static async Task WriteAsync(HttpContext context, int status, string message, string? code)
    {
        if (context.Response.HasStarted) return;
        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var payload = new
        {
            type = $"https://httpstatuses.io/{status}",
            title = status switch
            {
                400 => "Bad Request",
                401 => "Unauthorized",
                402 => "Payment Required",
                403 => "Forbidden",
                404 => "Not Found",
                409 => "Conflict",
                423 => "Locked",
                429 => "Too Many Requests",
                _ => "Server Error"
            },
            status,
            code,
            message,
            detail = message,
            traceId = context.TraceIdentifier
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
