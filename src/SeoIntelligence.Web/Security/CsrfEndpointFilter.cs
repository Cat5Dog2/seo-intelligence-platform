using Microsoft.AspNetCore.Antiforgery;

namespace SeoIntelligence.Web.Security;

/// <summary>
/// Validates the antiforgery token on state-changing account endpoints. Blazor's
/// <c>UseAntiforgery</c> middleware covers component form posts, but the minimal API endpoints
/// that sign in and change passwords are mapped outside that pipeline.
/// </summary>
public static class CsrfEndpointFilter
{
    public const string HeaderName = "X-CSRF-TOKEN";

    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Get,
        HttpMethods.Head,
        HttpMethods.Options,
        HttpMethods.Trace
    };

    public static TBuilder RequireCsrfToken<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(ValidateAsync);
        return builder;
    }

    private static async ValueTask<object?> ValidateAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        if (SafeMethods.Contains(httpContext.Request.Method))
        {
            return await next(context);
        }

        var antiforgery = httpContext.RequestServices.GetRequiredService<IAntiforgery>();
        try
        {
            await antiforgery.ValidateRequestAsync(httpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(
                title: "CSRF token validation failed.",
                detail: "CSRF token is missing or invalid.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return await next(context);
    }
}
