using PA.Tickets.Application.Reporting;
using PA.Tickets.Infrastructure.Reporting;
namespace PA.Tickets.Domain.Tests;
public sealed class ReportScheduleCalculatorTests
{
    [Fact] public void Monthly_schedule_clamps_month_end() { var next = ReportScheduleCalculator.Next(new DateTimeOffset(2026, 1, 31, 10, 0, 0, TimeSpan.Zero), new(ReportScheduleFrequency.Monthly, 9, 0, DayOfMonth: 31), TimeZoneInfo.Utc); Assert.Equal(new DateTimeOffset(2026, 2, 28, 9, 0, 0, TimeSpan.Zero), next); }
    [Fact] public void Weekly_schedule_advances_to_requested_weekday() { var next = ReportScheduleCalculator.Next(new DateTimeOffset(2026, 1, 5, 10, 0, 0, TimeSpan.Zero), new(ReportScheduleFrequency.Weekly, 9, 0, DayOfWeek.Friday), TimeZoneInfo.Utc); Assert.Equal(DayOfWeek.Friday, next.DayOfWeek); }
}