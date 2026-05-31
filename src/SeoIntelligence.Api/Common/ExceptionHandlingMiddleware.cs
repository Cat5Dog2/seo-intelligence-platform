using SeoIntelligence.Contracts.Api;

namespace SeoIntelligence.Api.Common;

internal sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (BadHttpRequestException exception)
        {
            logger.LogWarning(exception, "HTTP request rejected before endpoint execution.");

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            await ApiResponseResults.Failure(
                context,
                StatusCodes.Status400BadRequest,
                [new ApiError("Validation.Request.Invalid", "Request is invalid.")])
                .ExecuteAsync(context);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled API exception.");

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            await ApiResponseResults.Failure(
                context,
                StatusCodes.Status500InternalServerError,
                [new ApiError("Common.Unexpected", "An unexpected error occurred.")])
                .ExecuteAsync(context);
        }
    }
}
