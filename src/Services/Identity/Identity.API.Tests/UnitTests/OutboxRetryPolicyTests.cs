using Identity.API.Messaging.Outbox;

namespace Identity.API.Tests.UnitTests;

public class OutboxRetryPolicyTests
{
    [Theory]
    [InlineData(1, 30)]     // first failure: 30s
    [InlineData(2, 60)]     // doubles
    [InlineData(3, 120)]
    [InlineData(5, 480)]
    [InlineData(6, 900)]    // 30 * 2^5 = 960 -> capped at 900
    [InlineData(20, 900)]   // stays capped, no overflow
    public void NextDelay_doubles_and_caps_at_15_minutes(int attemptCount, int expectedSeconds)
    {
        OutboxRetryPolicy.NextDelay(attemptCount).ShouldBe(TimeSpan.FromSeconds(expectedSeconds));
    }
}
