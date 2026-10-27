using PA.Tickets.Application.Foundations;

namespace PA.Tickets.Domain.Tests;

public sealed class FeatureSwitchResolutionTests
{
    [Fact]
    public void MissingConfigurationFailsClosed()
    {
        Assert.False(FeatureSwitchResolution.IsEnabled(null, null));
    }

    [Fact]
    public void DepartmentOverrideTakesPrecedenceAndOtherwiseUsesGlobalValue()
    {
        Assert.True(FeatureSwitchResolution.IsEnabled(true, false));
        Assert.False(FeatureSwitchResolution.IsEnabled(false, true));
        Assert.True(FeatureSwitchResolution.IsEnabled(null, true));
        Assert.False(FeatureSwitchResolution.IsEnabled(null, false));
    }

    [Fact]
    public void Actor_availability_requires_an_enabled_matching_switch()
    {
        Assert.True(FeatureSwitchResolution.IsEnabledForActor("REALTIME",
            [new FeatureAvailabilityView("MESSAGE_CENTER", true), new FeatureAvailabilityView("REALTIME", true)]));
        Assert.False(FeatureSwitchResolution.IsEnabledForActor("REALTIME",
            [new FeatureAvailabilityView("REALTIME", false)]));
        Assert.False(FeatureSwitchResolution.IsEnabledForActor("REALTIME", []));
        Assert.False(FeatureSwitchResolution.IsEnabledForActor("REALTIME",
            [new FeatureAvailabilityView("UNKNOWN_SWITCH", true)]));
    }

    [Fact]
    public void SwitchCatalogueContainsOnlyTheGovernedCategories()
    {
        Assert.Equal(new[]
        {
            "ANNOUNCEMENTS", "CHANNEL_EMAIL", "CHANNEL_INAPP", "CHANNEL_PUSH", "CHANNEL_WHATSAPP",
            "CHAT", "CHAT_ATTACHMENTS", "MESSAGE_CENTER", "REALTIME"
        }, FeatureSwitchCodes.All.Keys.OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal("Real-time updates", FeatureSwitchCodes.All["REALTIME"]);
    }
}
