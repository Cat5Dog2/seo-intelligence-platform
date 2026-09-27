using SeoIntelligence.Application.Services;

namespace SeoIntelligence.Web.Services;

public sealed class ProjectSelectionState
{
    private readonly ISeoIntelligenceApiClient _apiClient;
    private readonly GuestApiRouter? _guestRouter;

    public ProjectSelectionState(ISeoIntelligenceApiClient apiClient, GuestApiRouter? guestRouter = null)
    {
        _apiClient = apiClient;
        _guestRouter = guestRouter;
    }

    public event Action? Changed;

    public IReadOnlyList<ProjectDetails> Projects { get; private set; } = [];

    public ProjectDetails? SelectedProject { get; private set; }

    public bool IsLoading { get; private set; }

    public string? ErrorMessage { get; private set; }

    public async Task LoadAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        if (!force && Projects.Count > 0)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        NotifyChanged();

        var result = await _apiClient.SearchProjectsAsync(pageSize: 100, cancellationToken: cancellationToken);
        if (result.IsSuccess)
        {
            Projects = result.Data ?? [];
            var session = _guestRouter is null ? null : await _guestRouter.GetSessionAsync();
            SelectedProject = SelectCurrentProject(SelectedProject?.ProjectId ?? session?.SelectedProjectId);
            if (SelectedProject is { } selected) session?.SelectProject(selected.ProjectId);
        }
        else
        {
            Projects = [];
            SelectedProject = null;
            ErrorMessage = result.ErrorSummary;
        }

        IsLoading = false;
        NotifyChanged();
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
        => await LoadAsync(force: true, cancellationToken);

    public async Task<bool> SelectFromQueryAsync(string? projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId)) return true;
        if (!Guid.TryParse(projectId, out var id) || !Projects.Any(project => project.ProjectId == id && project.Status == "active")) return false;
        if (SelectedProject?.ProjectId != id) await SelectAsync(id);
        return true;
    }

    public async Task SelectAsync(Guid projectId)
    {
        if (!Projects.Any(project => project.ProjectId == projectId && project.Status == "active")) return;
        SelectedProject = SelectCurrentProject(projectId);
        if (_guestRouter is not null) (await _guestRouter.GetSessionAsync())?.SelectProject(projectId);
        NotifyChanged();
    }

    private ProjectDetails? SelectCurrentProject(Guid? preferredProjectId)
    {
        if (Projects.Count == 0)
        {
            return null;
        }

        if (preferredProjectId.HasValue)
        {
            var preferred = Projects.FirstOrDefault(project => project.ProjectId == preferredProjectId.Value);
            if (preferred is not null)
            {
                return preferred;
            }
        }

        return Projects.FirstOrDefault(project => string.Equals(project.Status, "active", StringComparison.OrdinalIgnoreCase))
            ?? Projects[0];
    }

    private void NotifyChanged()
        => Changed?.Invoke();
}
