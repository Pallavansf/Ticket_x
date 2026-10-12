namespace PA.Tickets.Infrastructure.Execution;

public static class DurableJobRetryPolicy
{
    public static double DelaySeconds(int attempt, int initialDelaySeconds, int maximumDelaySeconds)
    {
        var initial = Math.Clamp(initialDelaySeconds, 1, 3600);
        var maximum = Math.Clamp(maximumDelaySeconds, initial, 86400);
        return Math.Min(maximum, initial * Math.Pow(2, Math.Min(20, Math.Max(0, attempt - 1))));
    }
}