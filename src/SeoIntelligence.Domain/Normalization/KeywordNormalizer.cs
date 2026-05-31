using System.Text;

namespace SeoIntelligence.Domain.Normalization;

public static class KeywordNormalizer
{
    public static string Normalize(string keyword)
        => keyword.Trim().Normalize(NormalizationForm.FormKC);

    public static IReadOnlyList<string> NormalizeMany(IEnumerable<string?> keywords)
    {
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var keyword in keywords)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                continue;
            }

            var value = Normalize(keyword);

            if (value.Length == 0 || !seen.Add(value))
            {
                continue;
            }

            normalized.Add(value);
        }

        return normalized;
    }
}
