using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShortLink.Application.Common.Exceptions;
using ShortLink.Domain.Exceptions;
using StackExchange.Redis;

namespace ShortLink.API.Middleware;

/// <summary>
/// Global exception handling middleware capturing all unhandled exceptions
/// and transforming them into standard RFC 7807 ProblemDetails responses.
/// Enriches responses with distributed TraceId and sanitizes internal system details.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
            _logger.LogError(ex, "Unhandled exception occurred. TraceId: {TraceId}. Message: {Message}", traceId, ex.Message);
            await HandleExceptionAsync(context, ex, traceId);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception, string traceId)
    {
        context.Response.ContentType = "application/problem+json";

        var (statusCode, problemDetails) = exception switch
        {
            ValidationException validationEx => (
                (int)HttpStatusCode.BadRequest,
                new ProblemDetails
                {
                    Status = (int)HttpStatusCode.BadRequest,
                    Title = "Validation Error",
                    Detail = "One or more validation errors occurred.",
                    Instance = context.Request.Path,
                    Extensions = { ["errors"] = validationEx.Errors, ["traceId"] = traceId }
                }
            ),
            DomainException domainEx => (
                (int)HttpStatusCode.BadRequest,
                new ProblemDetails
                {
                    Status = (int)HttpStatusCode.BadRequest,
                    Title = "Domain Rule Violation",
                    Detail = domainEx.Message,
                    Instance = context.Request.Path,
                    Extensions = { ["traceId"] = traceId }
                }
            ),
            NotFoundException notFoundEx => (
                (int)HttpStatusCode.NotFound,
                new ProblemDetails
                {
                    Status = (int)HttpStatusCode.NotFound,
                    Title = "Resource Not Found",
                    Detail = notFoundEx.Message,
                    Instance = context.Request.Path,
                    Extensions = { ["traceId"] = traceId }
                }
            ),
            DbUpdateException dbEx => (
                (int)HttpStatusCode.ServiceUnavailable,
                new ProblemDetails
                {
                    Status = (int)HttpStatusCode.ServiceUnavailable,
                    Title = "Database Operation Failed",
                    Detail = "A database constraint violation or transient persistence failure occurred.",
                    Instance = context.Request.Path,
                    Extensions = { ["traceId"] = traceId }
                }
            ),
            RedisException redisEx => (
                (int)HttpStatusCode.ServiceUnavailable,
                new ProblemDetails
                {
                    Status = (int)HttpStatusCode.ServiceUnavailable,
                    Title = "Cache Service Warning",
                    Detail = "Cache operation experienced a transient failure; falling back to persistence.",
                    Instance = context.Request.Path,
                    Extensions = { ["traceId"] = traceId }
                }
            ),
            _ => (
                (int)HttpStatusCode.InternalServerError,
                new ProblemDetails
                {
                    Status = (int)HttpStatusCode.InternalServerError,
                    Title = "Internal Server Error",
                    Detail = "An unexpected error occurred while processing your request. Please reference the TraceId when contacting support.",
                    Instance = context.Request.Path,
                    Extensions = { ["traceId"] = traceId }
                }
            )
        };

        context.Response.StatusCode = statusCode;
        var jsonResponse = JsonSerializer.Serialize(problemDetails);
        await context.Response.WriteAsync(jsonResponse);
    }
}
