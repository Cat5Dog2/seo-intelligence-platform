namespace SeoIntelligence.Contracts.Api;

public sealed class ListQueryParameters
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;
    public const int MaxSearchTextLength = 200;

    private static readonly HashSet<string> DefaultStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "active",
        "archived",
        "disabled",
        "all"
    };

    private static readonly HashSet<string> OrderDirections = new(StringComparer.OrdinalIgnoreCase)
    {
        "asc",
        "desc"
    };

    public int Page { get; init; } = DefaultPage;

    public int PageSize { get; init; } = DefaultPageSize;

    public string Status { get; init; } = "active";

    public string SortBy { get; init; } = "createdAt";

    public string OrderBy { get; init; } = "desc";

    public string? Q { get; init; }

    public IReadOnlyDictionary<string, string[]> Validate(
        IEnumerable<string>? allowedStatuses = null,
        IEnumerable<string>? allowedSortBy = null)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var statuses = ToLookupSet(allowedStatuses) ?? DefaultStatuses;
        var sortFields = ToLookupSet(allowedSortBy);

        if (Page < 1)
        {
            AddError(errors, nameof(Page), "page must be greater than or equal to 1.");
        }

        if (PageSize < 1 || PageSize > MaxPageSize)
        {
            AddError(errors, nameof(PageSize), $"pageSize must be between 1 and {MaxPageSize}.");
        }

        if (string.IsNullOrWhiteSpace(Status) || !statuses.Contains(Status.Trim()))
        {
            AddError(errors, nameof(Status), "status is not allowed.");
        }

        if (string.IsNullOrWhiteSpace(SortBy))
        {
            AddError(errors, nameof(SortBy), "sortBy is required.");
        }
        else if (sortFields is not null && !sortFields.Contains(SortBy.Trim()))
        {
            AddError(errors, nameof(SortBy), "sortBy is not allowed.");
        }

        if (string.IsNullOrWhiteSpace(OrderBy) || !OrderDirections.Contains(OrderBy.Trim()))
        {
            AddError(errors, nameof(OrderBy), "orderBy must be asc or desc.");
        }

        if (Q is { Length: > MaxSearchTextLength })
        {
            AddError(errors, nameof(Q), $"q must be {MaxSearchTextLength} characters or fewer.");
        }

        return errors.ToDictionary(
            pair => ToCamelCase(pair.Key),
            pair => pair.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string>? ToLookupSet(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return null;
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static void AddError(IDictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var messages))
        {
            messages = [];
            errors[key] = messages;
        }

        messages.Add(message);
    }

    private static string ToCamelCase(string value)
        => char.ToLowerInvariant(value[0]) + value[1..];
}
