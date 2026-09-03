namespace Identity.API.Tests.UnitTests;

public class VerificationCodeHasherTests
{
    [Fact]
    public void HashProduces64CharHexString()
    {
        // Arrange
        const string code = "ABC1234567";
        // Act
        var result = VerificationCodeHasher.Hash(code);
        
        // Assert
        result.Length.ShouldBe(64);
        result.ShouldMatch("^[0-9a-f]{64}$");
    }

    [Fact]
    public void HashIsDeterministic()
    {
        // Arrange
        var code = "ABC1234567";
        
        // Act & Assert
        VerificationCodeHasher.Hash(code).ShouldBe(VerificationCodeHasher.Hash(code));
    }

    [Fact]
    public void HashDifferentInputsProduceDifferentOutputs()
    {
        // Arrange & Act & Assert
        VerificationCodeHasher.Hash("ABC1234567").ShouldNotBe(VerificationCodeHasher.Hash("XYZ9876543"));
    }

    [Fact]
    public void HashOutputIsLowercaseHex()
    {
        // Act
        var result = VerificationCodeHasher.Hash("test");
        
        // Assert
        result.ShouldBe(result.ToLowerInvariant());
    }
}

public class VerificationCodeExpiryTests
{
    [Fact]
    public void IsExpiredReturnsFalseWhenCreatedNow()
    {
        // Act
        var code = BuildCode(DateTime.UtcNow);
        
        // Assert
        code.IsExpired.ShouldBeFalse();
    }

    [Fact]
    public void IsExpiredReturnsFalseWhenCreated29MinutesAgo()
    {
        // Act
        var code = BuildCode(DateTime.UtcNow.AddMinutes(-29));

        // Assert
        code.IsExpired.ShouldBeFalse();
    }

    [Fact]
    public void IsExpiredReturnsTrueWhenCreated31MinutesAgo()
    {
        // Act
        var code = BuildCode(DateTime.UtcNow.AddMinutes(-31));
        
        // Assert
        code.IsExpired.ShouldBeTrue();
    }

    [Fact]
    public void IsExpiredReturnsTrueWhenCreatedExactly30MinutesAgo()
    {
        // Act
        var code = BuildCode(DateTime.UtcNow.AddMinutes(-30).AddSeconds(-1));
        
        // Assert
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
