namespace Identity.API.Messaging.Outbox;

public static class OutboxRetryPolicy
{
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(15);

    public static TimeSpan NextDelay(int attemptCount)
    {
        var seconds = BaseDelay.TotalSeconds * Math.Pow(2, attemptCount - 1);
        return seconds >= MaxDelay.TotalSeconds ? MaxDelay : TimeSpan.FromSeconds(seconds);
    }
}
