using SeoIntelligence.Application.Services;

namespace SeoIntelligence.Web.Services;

public sealed class ProjectSelectionState
{
    private readonly ISeoIntelligenceApiClient _apiClient;

    public ProjectSelectionState(ISeoIntelligenceApiClient apiClient)
    {
        _apiClient = apiClient;
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
            SelectedProject = SelectCurrentProject(SelectedProject?.ProjectId);
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

    public Task SelectAsync(Guid projectId)
    {
        SelectedProject = SelectCurrentProject(projectId);
        NotifyChanged();
        return Task.CompletedTask;
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
