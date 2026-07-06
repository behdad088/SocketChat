using System.ComponentModel.DataAnnotations;

namespace Identity.API.Endpoints;

public sealed class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is null)
        {
            return await next(context);
        }

        var validationContext = new ValidationContext(argument);
        var validationResults = new List<ValidationResult>();
        if (Validator.TryValidateObject(argument, validationContext, validationResults, validateAllProperties: true))
        {
            return await next(context);
        }

        var errors = validationResults
            .SelectMany(r => r.MemberNames.DefaultIfEmpty(string.Empty).Select(m => (Member: m, r.ErrorMessage)))
            .GroupBy(x => x.Member)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage ?? "Invalid value.").ToArray());

        return Results.ValidationProblem(errors);
    }
}
