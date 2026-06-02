namespace SeoIntelligence.Web.Components.Common;

public sealed record DataTableColumn<TItem>(
    string Header,
    Func<TItem, string?> Cell,
    string? CssClass = null);
