namespace SeoIntelligence.Domain.Common;

public static class TokyoDateBoundary
{
    public static readonly TimeZoneInfo TimeZone = FindTokyoTimeZone();

    public static AggregationWindow DayWindow(DateTimeOffset instantUtc)
    {
        var localDate = DateOnly.FromDateTime(ToTokyoLocal(instantUtc).DateTime);
        var start = StartOfDayUtc(localDate);

        return new AggregationWindow(start, start.AddDays(1));
    }

    public static AggregationWindow MonthWindow(DateTimeOffset instantUtc)
    {
        var localDate = DateOnly.FromDateTime(ToTokyoLocal(instantUtc).DateTime);
        var start = StartOfMonthUtc(localDate.Year, localDate.Month);

        return new AggregationWindow(start, start.AddMonths(1));
    }

    public static DateTimeOffset StartOfDayUtc(DateOnly tokyoDate)
    {
        var localStart = tokyoDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(localStart, TimeZone.GetUtcOffset(localStart)).ToUniversalTime();
    }

    public static DateTimeOffset StartOfMonthUtc(int year, int month)
    {
        var localStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(localStart, TimeZone.GetUtcOffset(localStart)).ToUniversalTime();
    }

    private static DateTimeOffset ToTokyoLocal(DateTimeOffset instantUtc)
        => TimeZoneInfo.ConvertTime(UtcDateTime.EnsureUtc(instantUtc), TimeZone);

    private static TimeZoneInfo FindTokyoTimeZone()
    {
        foreach (var id in new[] { "Asia/Tokyo", "Tokyo Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("Asia/Tokyo", TimeSpan.FromHours(9), "Asia/Tokyo", "Asia/Tokyo");
    }
}

public sealed record AggregationWindow(DateTimeOffset StartUtc, DateTimeOffset EndUtc);
