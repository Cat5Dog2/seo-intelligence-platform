using System.Text.Json;

namespace SeoIntelligence.Infrastructure.RakkoKeyword;

internal static class RakkoKeywordJson
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
}
