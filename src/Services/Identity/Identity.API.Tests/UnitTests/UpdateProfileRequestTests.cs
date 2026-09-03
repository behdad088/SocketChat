using System.ComponentModel.DataAnnotations;
using Identity.API.Endpoints;

namespace Identity.API.Tests.UnitTests;

public class UpdateProfileRequestTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private static List<ValidationResult> Validate(UpdateProfileRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void ExplicitNullProfilePictureFailsValidation()
    {
        // Arrange
        var request = JsonSerializer.Deserialize<UpdateProfileRequest>(
            """{"name":"A","lastName":"B","profilePicture":null}""", WebOptions)!;

        // Act
        var results = Validate(request);

        // Assert
        results.ShouldContain(r => r.MemberNames.Contains(nameof(UpdateProfileRequest.ProfilePicture)));
    }

    [Fact]
    public void EmptyProfilePicturePassesValidation()
    {
        // Act
        var request = JsonSerializer.Deserialize<UpdateProfileRequest>(
            """{"name":"A","lastName":"B","profilePicture":""}""", WebOptions)!;

        // Assert
        Validate(request).ShouldBeEmpty();
    }

    [Fact]
    public void OmittedProfilePictureDefaultsToEmptyAndPassesValidation()
    {
        // Act
        var request = JsonSerializer.Deserialize<UpdateProfileRequest>(
            """{"name":"A","lastName":"B"}""", WebOptions)!;

        // Assert
        request.ProfilePicture.ShouldBe(string.Empty);
        Validate(request).ShouldBeEmpty();
    }
}
