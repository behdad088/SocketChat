using System.ComponentModel.DataAnnotations;
using System.Text.Json;
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
    public void Explicit_null_profile_picture_fails_validation()
    {
        // Mirrors minimal-API body binding: JSON null lands in the non-nullable property.
        var request = JsonSerializer.Deserialize<UpdateProfileRequest>(
            """{"name":"A","lastName":"B","profilePicture":null}""", WebOptions)!;

        var results = Validate(request);

        results.ShouldContain(r => r.MemberNames.Contains(nameof(UpdateProfileRequest.ProfilePicture)));
    }

    [Fact]
    public void Empty_profile_picture_passes_validation()
    {
        var request = JsonSerializer.Deserialize<UpdateProfileRequest>(
            """{"name":"A","lastName":"B","profilePicture":""}""", WebOptions)!;

        Validate(request).ShouldBeEmpty();
    }

    [Fact]
    public void Omitted_profile_picture_defaults_to_empty_and_passes_validation()
    {
        var request = JsonSerializer.Deserialize<UpdateProfileRequest>(
            """{"name":"A","lastName":"B"}""", WebOptions)!;

        request.ProfilePicture.ShouldBe(string.Empty);
        Validate(request).ShouldBeEmpty();
    }
}
