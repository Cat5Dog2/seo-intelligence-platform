using System.Text;

namespace SeoIntelligence.Web.Services;

public static class KeywordInputParser
{
    public const int MaxKeywordCount = 50_000;

    public static KeywordInputParseResult Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new KeywordInputParseResult([], 0, 0, 0);
        }

        var keywords = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var blankCount = 0;
        var duplicateCount = 0;
        var totalFieldCount = 0;

        foreach (var field in ReadCsvFields(input))
        {
            totalFieldCount++;
            if (string.IsNullOrWhiteSpace(field))
            {
                blankCount++;
                continue;
            }

            var keyword = field.Trim().Normalize(NormalizationForm.FormKC);
            if (keyword.Length == 0)
            {
                blankCount++;
                continue;
            }

            if (!seen.Add(keyword))
            {
                duplicateCount++;
                continue;
            }

            keywords.Add(keyword);
        }

        return new KeywordInputParseResult(keywords, blankCount, duplicateCount, totalFieldCount);
    }

    private static IEnumerable<string> ReadCsvFields(string input)
    {
        var field = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < input.Length; index++)
        {
            var current = input[index];
            if (inQuotes)
            {
                if (current == '"')
                {
                    if (index + 1 < input.Length && input[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(current);
                }

                continue;
            }

            if (current == '"')
            {
                inQuotes = true;
                continue;
            }

            if (current == ',' || current == '\r' || current == '\n')
            {
                yield return field.ToString();
                field.Clear();

                if (current == '\r' && index + 1 < input.Length && input[index + 1] == '\n')
                {
                    index++;
                }

                continue;
            }

            field.Append(current);
        }

        yield return field.ToString();
    }
}

public sealed record KeywordInputParseResult(
    IReadOnlyList<string> Keywords,
    int BlankCount,
    int DuplicateCount,
    int TotalFieldCount);
