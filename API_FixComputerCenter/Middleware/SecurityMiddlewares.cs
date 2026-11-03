using System.Net;
using System.Text.Json;
using API_FixComputerCenter.Application.Common.Exceptions;
using API_FixComputerCenter.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API_FixComputerCenter.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Instance = context.Request.Path
        };

        switch (exception)
        {
            case ValidationException valEx:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                problemDetails.Title = "Validation Error";
                problemDetails.Status = (int)HttpStatusCode.BadRequest;
                problemDetails.Detail = valEx.Message;
                problemDetails.Extensions["errors"] = valEx.Errors;
                break;

            case DomainException domainEx:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                problemDetails.Title = "Business Rule Violation";
                problemDetails.Status = (int)HttpStatusCode.BadRequest;
                problemDetails.Detail = domainEx.Message;
                break;

            case NotFoundException notFoundEx:
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                problemDetails.Title = "Resource Not Found";
                problemDetails.Status = (int)HttpStatusCode.NotFound;
                problemDetails.Detail = notFoundEx.Message;
                break;

            case ForbiddenException forbiddenEx:
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                problemDetails.Title = "Forbidden";
                problemDetails.Status = (int)HttpStatusCode.Forbidden;
                problemDetails.Detail = forbiddenEx.Message;
                break;

            case UnauthorizedException unauthEx:
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                problemDetails.Title = "Unauthorized";
                problemDetails.Status = (int)HttpStatusCode.Unauthorized;
                problemDetails.Detail = unauthEx.Message;
                break;

            default:
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                problemDetails.Title = "Internal Server Error";
                problemDetails.Status = (int)HttpStatusCode.InternalServerError;
                // Never leak internal stack trace in Production
                problemDetails.Detail = _env.IsDevelopment() ? exception.ToString() : "An unexpected error occurred. Please contact system support.";
                break;
        }

        var json = JsonSerializer.Serialize(problemDetails);
        return context.Response.WriteAsync(json);
    }
}

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");

        await _next(context);
    }
}

