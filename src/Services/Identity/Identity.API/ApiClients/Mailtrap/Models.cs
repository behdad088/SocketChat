using System.Text.Json.Serialization;

namespace Identity.API.ApiClients.Mailtrap;

public static class EmailServiceConstants
{
    public const string VerifyEmailTemplateId = "fb8b709b-c640-4744-879b-11ec7ccf0f58";
    public const string ForgotPasswordTemplateId = "95c0e528-95b5-4dd6-81c3-65ff802afa0f";
    public const string EmailVerificationType = "email_verification";
    public const string ForgotPasswordType = "forgot_password";
}

public record SendVerificationEmailRequest(
    [property: JsonPropertyName("from")]
    From From,
    [property: JsonPropertyName("to")]
    To[] To,
    [property: JsonPropertyName("template_uuid")]
    string TemplateUuid,
    [property: JsonPropertyName("template_variables")]
    TemplateVariables Variables
    );
public record From(
    [property: JsonPropertyName("email")]
    string Email,
    [property: JsonPropertyName("name")]
    string Name = "Identity Service");

public record To(
    [property: JsonPropertyName("email")]
    string Email);


public record TemplateVariables(
    [property: JsonPropertyName("company_info_name")]
    string CompanyInfoName,
    [property: JsonPropertyName("customer_name")] 
    string CustomerName,
    [property: JsonPropertyName("verification_link")]
    string VerificationLink);
    
public record SendVerificationEmailResponse(
        [property: JsonPropertyName("success")]
        bool Success);