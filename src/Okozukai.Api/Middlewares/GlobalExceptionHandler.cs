using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Okozukai.Api.Middlewares;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(
            exception, "An error occurred: {Message}", exception.Message);

        var problemDetails = new ProblemDetails
        {
            Instance = httpContext.Request.Path,
            Extensions = new Dictionary<string, object?>
            {
                ["traceId"] = httpContext.TraceIdentifier
            }
        };

        if (exception is ArgumentOutOfRangeException outOfRangeException)
        {
            problemDetails.Title = "Bad Request - Out of Range";
            problemDetails.Status = StatusCodes.Status400BadRequest;
            problemDetails.Detail = outOfRangeException.Message;
        }
        else if (exception is ArgumentException argumentException)
        {
            problemDetails.Title = "Bad Request";
            problemDetails.Status = StatusCodes.Status400BadRequest;
            problemDetails.Detail = argumentException.Message;
        }
        else if (exception is KeyNotFoundException keyNotFoundException)
        {
            problemDetails.Title = "Not Found";
            problemDetails.Status = StatusCodes.Status404NotFound;
            problemDetails.Detail = keyNotFoundException.Message;
        }
        else if (exception is InvalidOperationException invalidOperationException)
        {
            problemDetails.Title = "Conflict";
            problemDetails.Status = StatusCodes.Status409Conflict;
            problemDetails.Detail = invalidOperationException.Message;
        }
        else
        {
            problemDetails.Title = "Internal Server Error";
            problemDetails.Status = StatusCodes.Status500InternalServerError;
            problemDetails.Detail = "An unexpected error occurred.";
        }

        httpContext.Response.StatusCode = problemDetails.Status.Value;

        await httpContext.Response
            .WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
