using PA.Tickets.Infrastructure.Foundations;

namespace PA.Tickets.Domain.Tests;

public sealed class EffectiveGrantAuthorizationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Unknown_date_or_status_never_authorizes()
    {
        Assert.False(EffectiveGrantAuthorization.IsEffective(true, "UNKNOWN", null, null, Now));
        Assert.False(EffectiveGrantAuthorization.IsEffective(true, "UNKNOWN", Now.AddDays(-1), null, Now));
        Assert.False(EffectiveGrantAuthorization.IsEffective(true, "ACTIVE", null, null, Now));
        Assert.False(EffectiveGrantAuthorization.IsEffective(false, "ACTIVE", Now.AddDays(-1), null, Now));
    }

    [Fact]
    public void Known_active_grant_is_inclusive_at_start_and_exclusive_at_expiration()
    {
        var start = Now;
        var end = Now.AddHours(1);
        Assert.True(EffectiveGrantAuthorization.IsEffective(true, "ACTIVE", start, end, start));
        Assert.True(EffectiveGrantAuthorization.IsEffective(true, "ACTIVE", start, end, end.AddTicks(-1)));
        Assert.False(EffectiveGrantAuthorization.IsEffective(true, "ACTIVE", start, end, end));
    }

    [Fact]
    public void Future_expired_revoked_and_unknown_status_grants_are_rejected()
    {
        Assert.False(EffectiveGrantAuthorization.IsEffective(true, "ACTIVE", Now.AddTicks(1), null, Now));
        Assert.False(EffectiveGrantAuthorization.IsEffective(true, "ACTIVE", Now.AddDays(-2), Now.AddTicks(-1), Now));
        Assert.False(EffectiveGrantAuthorization.IsEffective(true, "EXPIRED", Now.AddDays(-2), null, Now));
        Assert.False(EffectiveGrantAuthorization.IsEffective(true, "REVOKED", Now.AddDays(-2), null, Now));
    }

    [Fact]
    public void Comparisons_are_offset_aware_and_do_not_use_local_time()
    {
        var equivalentInstant = new DateTimeOffset(2026, 10, 16, 17, 30, 0, TimeSpan.FromHours(5.5));
        Assert.True(EffectiveGrantAuthorization.IsEffective(true, "ACTIVE", equivalentInstant, null, Now));
    }
}
