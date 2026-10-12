using System.Net.Http;
using PA.Tickets.Infrastructure.Execution;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Domain.Tests;

public sealed class OperationalReliabilityUnitTests
{
    [Fact]
    public void Retry_backoff_grows_and_is_bounded()
    {
        Assert.Equal(5, DurableJobRetryPolicy.DelaySeconds(1, 5, 60));
        Assert.Equal(10, DurableJobRetryPolicy.DelaySeconds(2, 5, 60));
        Assert.Equal(60, DurableJobRetryPolicy.DelaySeconds(20, 5, 60));
        Assert.Equal(1, DurableJobRetryPolicy.DelaySeconds(1, -1, 0));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Job_failure_classifier_distinguishes_transient_and_permanent_errors(bool transient)
    {
        Exception error = transient ? new HttpRequestException("temporary") : new ArgumentException("invalid payload");
        Assert.Equal(transient, BackgroundJobFailureClassifier.IsRetryable(error, PostgreSqlProviderCapabilities.Instance));
    }
}