namespace Identity.API.Services.Account;

public record RegisteredAccount(string UserId, string Email);
public record UserProfile(
    string UserName,
    string Email,
    string? Name,
    string? LastName,
    string ProfilePicture,
    int Version);

public interface IAccountService
{
    Task<AccountOperationResult<RegisteredAccount>> RegisterAsync(string email, string password, string displayName = "");
    Task<AccountOperationResult> ResendVerificationEmailAsync(string email);
    Task<AccountOperationResult> ForgotPasswordAsync(string email, string? returnUrl);
    Task<AccountOperationResult<string>> ValidateResetCodeAsync(string code);
    Task<AccountOperationResult> ResetPasswordAsync(string userId, string code, string password);
    Task<AccountOperationResult> VerifyEmailAsync(string code);
    Task<AccountOperationResult<UserProfile>> GetProfileAsync(string userId);
    Task<AccountOperationResult<UserProfile>> UpdateProfileAsync(
        string userId, int expectedVersion, string? name, string? lastName, string profilePicture);
}
