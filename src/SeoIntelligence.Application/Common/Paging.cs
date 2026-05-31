namespace SeoIntelligence.Application.Common;

public sealed record PageRequest(int Page = 1, int PageSize = 50)
{
    public const int MaxPageSize = 200;

    public int Offset => (Page - 1) * PageSize;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (Page < 1)
        {
            errors.Add("page must be greater than or equal to 1.");
        }

        if (PageSize < 1 || PageSize > MaxPageSize)
        {
            errors.Add($"pageSize must be between 1 and {MaxPageSize}.");
        }

        return errors;
    }
}

public enum SortDirection
{
    Asc,
    Desc
}

public sealed record SortRequest(string? SortBy = null, SortDirection Direction = SortDirection.Desc);

public sealed record SearchQuery(
    string? Q = null,
    string? Status = null,
    SortRequest? Sort = null,
    PageRequest? Page = null)
{
    public PageRequest EffectivePage => Page ?? new PageRequest();
}

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    long TotalCount)
{
    public long TotalPages => TotalCount == 0 ? 0 : (long)Math.Ceiling((double)TotalCount / PageSize);
}
