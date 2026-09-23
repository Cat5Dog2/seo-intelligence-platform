using System.Text.Json;
using System.Text.Json.Serialization;

namespace SeoIntelligence.Infrastructure.RakkoKeyword;

internal static class RakkoKeywordJson
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static readonly JsonSerializerOptions RequestSerializerOptions = new(SerializerOptions)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
