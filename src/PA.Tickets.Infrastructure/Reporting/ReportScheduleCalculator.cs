using PA.Tickets.Application.Reporting;

namespace PA.Tickets.Infrastructure.Reporting;

public static class ReportScheduleCalculator
{
    public static DateTimeOffset Next(DateTimeOffset currentUtc, ReportScheduleRecurrence recurrence, TimeZoneInfo zone)
    {
        recurrence.Validate();
        var local = TimeZoneInfo.ConvertTime(currentUtc, zone).DateTime;
        var date = recurrence.Frequency switch
        {
            ReportScheduleFrequency.Daily => local.Date.AddDays(1),
            ReportScheduleFrequency.Weekly => NextWeekday(local.Date, recurrence.DayOfWeek!.Value),
            ReportScheduleFrequency.Monthly => NextMonth(local.Date, recurrence.DayOfMonth!.Value),
            _ => throw new ArgumentOutOfRangeException()
        };
        var desired = date.AddHours(recurrence.LocalHour).AddMinutes(recurrence.LocalMinute);
        if (recurrence.Frequency == ReportScheduleFrequency.Daily && desired <= local) desired = desired.AddDays(1);
        return ResolveLocal(desired, zone);
    }

    private static DateTime NextWeekday(DateTime date, DayOfWeek target)
    {
        var delta = ((int)target - (int)date.DayOfWeek + 7) % 7;
        return date.AddDays(delta == 0 ? 7 : delta);
    }
    private static DateTime NextMonth(DateTime date, int day)
    {
        var month = date.AddMonths(1);
        return new DateTime(month.Year, month.Month, Math.Min(day, DateTime.DaysInMonth(month.Year, month.Month)));
    }
    private static DateTimeOffset ResolveLocal(DateTime local, TimeZoneInfo zone)
    {
        if (zone.IsInvalidTime(local)) local = local.AddHours(1);
        var offset = zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }
}