using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using SeoIntelligence.Web.Security;

namespace SeoIntelligence.Web.Components.Common;

// Presentation only: the API router remains the authority for guest permissions.
public abstract class WorkspaceComponentBase : ComponentBase
{
    [CascadingParameter]
    public Task<AuthenticationState>? AuthenticationState { get; set; }

    protected bool IsGuest { get; private set; }

    protected override async Task OnInitializedAsync()
    {
        IsGuest = AuthenticationState is not null && GuestAuthentication.IsGuest((await AuthenticationState).User);
    }
}
