using System.Globalization;
using System.Net;

namespace SeoIntelligence.Infrastructure.RakkoKeyword;

internal static class RakkoKeywordClientSupport
{
    public const string SuggestKeywordsEndpoint = "/v1/suggest-keywords";
    public const string RelatedKeywordsEndpoint = "/v1/related-keywords";
    public const string OtherKeywordsEndpoint = "/v1/other-keywords";
    public const string QuestionSearchEndpoint = "/v1/question-search";
    public const string RankingKeywordsEndpoint = "/v1/ranking-keywords";
    public const string SearchVolumeEndpoint = "/v1/search-volume";
    public const string SearchVolumeStatusEndpoint = "/v1/search-volume/{requestId}/status";
    public const string SearchVolumeResultsEndpoint = "/v1/search-volume/{requestId}/results";
    public const string LocationsEndpoint = "/v1/search-volume/locations";
    public const string LanguagesEndpoint = "/v1/search-volume/languages";
    public const string InfluxKeywordsEndpoint = "/v1/influx-keywords";
    public const string InfluxPagesEndpoint = "/v1/influx-pages";
    public const string CompetitiveEndpoint = "/v1/competitive";
    public const string ContentSearchEndpoint = "/v1/content-search";
    public const string HeadlineEndpoint = "/v1/headline";
    public const string CoOccurrenceEndpoint = "/v1/co-occurrence";
    public const string SearchRankEndpoint = "/v1/search-rank";
    public const string SearchRankStatusEndpoint = "/v1/search-rank/{requestId}/status";
    public const string SearchRankResultsEndpoint = "/v1/search-rank/{requestId}/results";

    public static string SearchVolumeStatusPath(long requestId)
        => $"/v1/search-volume/{requestId}/status";

    public static string SearchVolumeResultsPath(long requestId)
        => $"/v1/search-volume/{requestId}/results";

    public static string SearchRankStatusPath(string requestId)
        => $"/v1/search-rank/{Uri.EscapeDataString(requestId)}/status";

    public static string SearchRankResultsPath(string requestId)
        => $"/v1/search-rank/{Uri.EscapeDataString(requestId)}/results";

    public static bool IsSuccessStatusCode(int statusCode)
        => statusCode is >= 200 and <= 299;

    public static string ToErrorCode(int statusCode)
        => statusCode switch
        {
            400 => "bad_request",
            402 => "credit_insufficient",
            403 => "forbidden",
            429 => "rate_limited",
            500 => "external_500",
            503 => "external_503",
            _ when statusCode >= 500 => $"external_{statusCode.ToString(CultureInfo.InvariantCulture)}",
            _ => $"http_{statusCode.ToString(CultureInfo.InvariantCulture)}"
        };

    public static Application.RakkoKeyword.RakkoKeywordFailureKind ToFailureKind(int statusCode)
        => statusCode is 429 or 500 or 503 || statusCode >= 500
            ? Application.RakkoKeyword.RakkoKeywordFailureKind.Retryable
            : Application.RakkoKeyword.RakkoKeywordFailureKind.Fatal;

    public static string DefaultErrorMessage(int statusCode)
        => statusCode switch
        {
            400 => "Invalid request parameters.",
            402 => "Insufficient credits.",
            403 => "Forbidden.",
            429 => "Rate limit exceeded.",
            500 => "Internal Server Error.",
            503 => "Service Unavailable.",
            _ => ((HttpStatusCode)statusCode).ToString()
        };
}
