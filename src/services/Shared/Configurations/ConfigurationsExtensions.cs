using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReHackt.Extensions.Options.Validation;


namespace Shared.Configurations;


public static class ConfigurationsExtensions
{
    public static bool TrySetConfiguration<T>(
        this IServiceCollection services,
        ConfigurationManager configuration,
        [NotNullWhen(true)] out T config) where T : class
    {
        services
            .AddOptions<T>()
            .Bind(configuration)
            .ValidateDataAnnotationsRecursively()
            .ValidateOnStart();
        
        var discountGrpcConfiguration = configuration.TryGetValidatedOptions<T>();
        config = discountGrpcConfiguration;
        
        return true;
    }
    
    public static T TryGetValidatedOptions<T>(this ConfigurationManager configurationManager) where T : class
    {
        var options = configurationManager.Get<T>() ??
                      throw new InvalidOperationException($"Failed resolving {typeof(T).FullName}.");
        
        var validator = new DataAnnotationsValidateRecursiveOptions<T>(string.Empty);
        var validationResult = validator.Validate(string.Empty, options);

        if (validationResult.Succeeded) return options;
        
        throw new InvalidOperationException(
            $"Failed validating {typeof(T).FullName} due to following failures : {validationResult.FailureMessage}");
    }
}