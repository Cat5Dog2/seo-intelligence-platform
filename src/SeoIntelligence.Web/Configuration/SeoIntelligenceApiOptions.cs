namespace SeoIntelligence.Web.Configuration;

public sealed class SeoIntelligenceApiOptions
{
    public const string SectionName = "Api";

    public string BaseUrl { get; set; } = "http://localhost:5251";
}
