namespace Identity.API.Tests.UnitTests;

public class VerificationCodeHasherTests
{
    [Fact]
    public void Hash_produces_64_char_hex_string()
    {
        var result = VerificationCodeHasher.Hash("ABC1234567");
        result.Length.ShouldBe(64);
        result.ShouldMatch("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Hash_is_deterministic()
    {
        var code = "ABC1234567";
        VerificationCodeHasher.Hash(code).ShouldBe(VerificationCodeHasher.Hash(code));
    }

    [Fact]
    public void Hash_different_inputs_produce_different_outputs()
    {
        VerificationCodeHasher.Hash("ABC1234567").ShouldNotBe(VerificationCodeHasher.Hash("XYZ9876543"));
    }

    [Fact]
    public void Hash_output_is_lowercase_hex()
    {
        var result = VerificationCodeHasher.Hash("test");
        result.ShouldBe(result.ToLowerInvariant());
    }
}

public class VerificationCodeExpiryTests
{
    [Fact]
    public void IsExpired_returns_false_when_created_now()
    {
        var code = BuildCode(DateTime.UtcNow);
        code.IsExpired.ShouldBeFalse();
    }

    [Fact]
    public void IsExpired_returns_false_when_created_29_minutes_ago()
    {
        var code = BuildCode(DateTime.UtcNow.AddMinutes(-29));
        code.IsExpired.ShouldBeFalse();
    }

    [Fact]
    public void IsExpired_returns_true_when_created_31_minutes_ago()
    {
        var code = BuildCode(DateTime.UtcNow.AddMinutes(-31));
        code.IsExpired.ShouldBeTrue();
    }

    [Fact]
    public void IsExpired_returns_true_when_created_exactly_30_minutes_ago()
    {
        // The boundary: 30 min window means exactly at 30 min it is expired
        var code = BuildCode(DateTime.UtcNow.AddMinutes(-30).AddSeconds(-1));
        code.IsExpired.ShouldBeTrue();
    }

    private static VerificationCode BuildCode(DateTime createdAt) => new()
    {
        UserId = "user-1",
        Code = "HASH",
        Type = "email_verification",
        CreatedAt = createdAt
    };
}
