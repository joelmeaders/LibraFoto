using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Diagnostics;

namespace LibraFoto.Api.Infrastructure
{
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
            if (exception is OperationCanceledException)
            {
                _logger.LogInformation("Request was canceled");
                httpContext.Response.StatusCode = 499; // Client Closed Request
                return true;
            }

            _logger.LogError(exception, "An unhandled exception occurred");

            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

            var error = new ApiError("InternalServerError", "An unexpected error occurred.");

            await httpContext.Response.WriteAsJsonAsync(
                error,
                cancellationToken: cancellationToken);

            return true;
        }
    }
}
