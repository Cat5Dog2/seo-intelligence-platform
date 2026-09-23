using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using SeoIntelligence.Application.Security;
using SeoIntelligence.Contracts.Api;
using SeoIntelligence.Web.Security;

namespace SeoIntelligence.Web.Services;

public sealed class GuestApiRouter(AuthenticationStateProvider authenticationState, GuestDemoSessionStore sessions)
{
    private ClaimsPrincipal? _requestPrincipal;

    // Regular HTTP endpoints have no Blazor renderer to initialize AuthenticationStateProvider.
    // This router is scoped, so this principal stays within that download request.
    internal void UseRequestPrincipal(ClaimsPrincipal principal) => _requestPrincipal = principal;

    public async Task<ApiClientResult<T>?> RouteAsync<T>(
        HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var principal = _requestPrincipal ?? (await authenticationState.GetAuthenticationStateAsync()).User;
        if (GuestAuthentication.IsGuest(principal))
        {
            var session = sessions.Find(principal);
            return session is null
                ? ApiClientResult<T>.Failure([new ApiError("Guest.SessionExpired", "デモの有効期限が切れました。ゲストログインし直してください。")], statusCode: HttpStatusCode.Unauthorized)
                : session.Execute<T>(method, path, body);
        }

        // Null is reserved for a real administrator and is the only route to the HTTP client.
        return principal.Identity?.IsAuthenticated == true && principal.IsInRole(ApplicationRoles.Admin)
            ? null
            : ApiClientResult<T>.Failure([new ApiError("Auth.Forbidden", "この操作を行う権限がありません。")], statusCode: HttpStatusCode.Forbidden);
    }
}
