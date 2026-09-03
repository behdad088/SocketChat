using System.Globalization;
using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.Net.Http.Headers;

namespace Shared;


public static partial class ValidationExtensions
{
    public static void MustBeValidEtag<T>(
        this IRuleBuilder<T, string?> ruleBuilder
    ) => ruleBuilder.Must(x => x is not null && EtagRegex().IsMatch(x))
        .WithMessage($"{HeaderNames.IfMatch} header is not valid.");


    public static void MustBeValidUlid<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder.NotEmpty().Must(x => Ulid.TryParse(x, out _))
            .WithMessage((_, propertyValue) => $"{propertyValue} is not a valid Ulid.");

    public static IRuleBuilderOptions<T, string?> MustBeValidGuid<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder.NotEmpty().Must(x => Guid.TryParse(x, out _))
            .WithMessage((_, propertyValue) => $"{propertyValue} is not a valid UUID");

    public static void MustBeValidTimestamp<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder.NotEmpty().Must(BeValidTimestamp)
        .WithMessage((_, value) => $"{value} is not a valid timestamp");
    
    private static bool BeValidTimestamp(string? timestamp)
    {
        return DateTime.TryParseExact(
            timestamp,
            "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out _
        );
    }

    [GeneratedRegex("""^W\/"\d+"$""")]
    private static partial Regex EtagRegex();
}