using PA.Tickets.Application.Reporting;
namespace PA.Tickets.Domain.Tests;
public sealed class ReportScheduleContractsTests
{
    [Fact] public void Weekly_recurrence_requires_day_of_week() => Assert.Throws<ArgumentException>(() => new ReportScheduleRecurrence(ReportScheduleFrequency.Weekly, 9, 0).Validate());
    [Fact] public void Monthly_recurrence_rejects_invalid_day() => Assert.Throws<ArgumentException>(() => new ReportScheduleRecurrence(ReportScheduleFrequency.Monthly, 9, 0, DayOfMonth: 32).Validate());
    [Fact] public void Daily_recurrence_accepts_business_time() => new ReportScheduleRecurrence(ReportScheduleFrequency.Daily, 9, 30).Validate();
}