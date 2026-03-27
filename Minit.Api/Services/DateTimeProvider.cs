namespace Minit.Api.Services;

public static class DateTimeProvider
{
    public static DateTime UtcNow => DateTime.UtcNow;

    public static int ToMonthYYYYMM(DateTime utcDate)
    {
        var normalized = utcDate.Kind == DateTimeKind.Utc ? utcDate : utcDate.ToUniversalTime();
        return (normalized.Year * 100) + normalized.Month;
    }
}
