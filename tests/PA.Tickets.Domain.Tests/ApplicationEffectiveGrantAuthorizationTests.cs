using ApplicationEffectiveGrantAuthorization = PA.Tickets.Application.Foundations.EffectiveGrantAuthorization;

namespace PA.Tickets.Domain.Tests;

public sealed class ApplicationEffectiveGrantAuthorizationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Active_grant_is_valid_at_start_and_invalid_at_end()
    {
        var end = Now.AddHours(1);
        Assert.True(ApplicationEffectiveGrantAuthorization.IsEffective(true, "ACTIVE", Now, end, Now));
        Assert.True(ApplicationEffectiveGrantAuthorization.IsEffective(true, "ACTIVE", Now, end, end.AddTicks(-1)));
        Assert.False(ApplicationEffectiveGrantAuthorization.IsEffective(true, "ACTIVE", Now, end, end));
    }

    [Theory]
    [InlineData(false, "ACTIVE")]
    [InlineData(true, "UNKNOWN")]
    [InlineData(true, "EXPIRED")]
    [InlineData(true, "REVOKED")]
    public void Inactive_or_non_active_status_is_denied(bool active, string status) =>
        Assert.False(ApplicationEffectiveGrantAuthorization.IsEffective(active, status, Now.AddDays(-1), null, Now));

    [Fact]
    public void Missing_or_future_start_and_elapsed_end_are_denied()
    {
        Assert.False(ApplicationEffectiveGrantAuthorization.IsEffective(true, "ACTIVE", null, null, Now));
        Assert.False(ApplicationEffectiveGrantAuthorization.IsEffective(true, "ACTIVE", Now.AddTicks(1), null, Now));
        Assert.False(ApplicationEffectiveGrantAuthorization.IsEffective(true, "ACTIVE", Now.AddDays(-1), Now, Now));
    }

    [Fact]
    public void Offset_equivalent_instants_and_open_end_are_supported()
    {
        var equivalentStart = new DateTimeOffset(2026, 10, 16, 17, 30, 0, TimeSpan.FromHours(5.5));
        Assert.True(ApplicationEffectiveGrantAuthorization.IsEffective(true, "active", equivalentStart, null, Now));
    }
}
