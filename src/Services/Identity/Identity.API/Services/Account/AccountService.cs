using System.Security.Claims;
using Duende.IdentityModel;
using Identity.API.Data;
using Identity.API.Models;
using Identity.API.Services.EmailService;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.Services.Account;

public class AccountService : IAccountService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IVerificationEmailService _verificationEmailService;
    private readonly ApplicationDbContext _dbContext;

    public AccountService(
        UserManager<ApplicationUser> userManager,
        IVerificationEmailService verificationEmailService,
        ApplicationDbContext dbContext)
    {
        _userManager = userManager;
        _verificationEmailService = verificationEmailService;
        _dbContext = dbContext;
    }

    public async Task<AccountOperationResult<RegisteredAccount>> RegisterAsync(
        string email, 
        string password,
        string displayName = "")
    {
        var normalizedEmail = email.ToLowerInvariant();
        var existing = await _userManager.FindByNameAsync(normalizedEmail);
        if (existing != null)
        {
            return AccountOperationResult<RegisteredAccount>.Failure(
                AccountErrorCode.Conflict, "Username already exists.");
        }

        var user = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            EmailConfirmed = false,
            Name = string.IsNullOrEmpty(displayName) ? email.Split('@')[0] : displayName
        };

        var createResult = await _userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            return AccountOperationResult<RegisteredAccount>.Failure(
                AccountErrorCode.ValidationFailed,
                createResult.Errors.Select(e => e.Description).ToArray());
        }

        var roleResult = await _userManager.AddToRoleAsync(user, Config.Roles.Customer);
        if (!roleResult.Succeeded)
        {
            return AccountOperationResult<RegisteredAccount>.Failure(
                AccountErrorCode.ValidationFailed, "Failed to add user to role.");
        }

        var claimsResult = await _userManager.AddClaimsAsync(user, [new Claim(JwtClaimTypes.Email, normalizedEmail)]);
        if (!claimsResult.Succeeded)
        {
            throw new InvalidOperationException(claimsResult.Errors.First().Description);
        }

        await _verificationEmailService.SendEmailAsync(user.Email, user.Id, EmailType.EmailVerification);

        return AccountOperationResult<RegisteredAccount>.Success(new RegisteredAccount(user.Id, normalizedEmail));
    }

    public async Task<AccountOperationResult> ResendVerificationEmailAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email.ToLowerInvariant());
        if (user is not null && !user.EmailConfirmed)
        {
            await _verificationEmailService.SendEmailAsync(user.Email!, user.Id, EmailType.EmailVerification);
        }

        return AccountOperationResult.Success();
    }

    public async Task<AccountOperationResult> ForgotPasswordAsync(string email, string? returnUrl)
    {
        var user = await _userManager.FindByEmailAsync(email.ToLowerInvariant());
        if (user is not null)
        {
            await _verificationEmailService.SendEmailAsync(email, user.Id, EmailType.ForgotPassword, returnUrl);
        }

        return AccountOperationResult.Success();
    }

    public async Task<AccountOperationResult<string>> ValidateResetCodeAsync(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return AccountOperationResult<string>.Failure(AccountErrorCode.InvalidCode, "Code is missing.");
        }

        var hashedCode = VerificationCodeHasher.Hash(code);
        var verificationCode = await _dbContext.VerificationCodes
            .FirstOrDefaultAsync(x => x.Code == hashedCode);

        var error = ValidateVerificationCode(verificationCode);
        if (error is not null)
        {
            return AccountOperationResult<string>.Failure(error.Value.Code, error.Value.Message);
        }

        return AccountOperationResult<string>.Success(verificationCode!.UserId);
    }

    public async Task<AccountOperationResult> ResetPasswordAsync(string userId, string code, string password)
    {
        var hashedCode = VerificationCodeHasher.Hash(code);
        var verificationCode = await _dbContext.VerificationCodes
            .FirstOrDefaultAsync(x => x.Code == hashedCode);

        var error = ValidateVerificationCode(verificationCode);
        if (error is not null)
        {
            return AccountOperationResult.Failure(error.Value.Code, error.Value.Message);
        }

        if (verificationCode!.UserId != userId)
        {
            return AccountOperationResult.Failure(AccountErrorCode.InvalidCode, "Invalid verification Code.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return AccountOperationResult.Failure(AccountErrorCode.NotFound, "User not found.");
        }

        await _userManager.RemovePasswordAsync(user);
        var result = await _userManager.AddPasswordAsync(user, password);
        if (!result.Succeeded)
        {
            return AccountOperationResult.Failure(
                AccountErrorCode.ValidationFailed,
                result.Errors.Select(e => e.Description).ToArray());
        }

        verificationCode.IsActivated = true;
        _dbContext.VerificationCodes.Update(verificationCode);
        await _dbContext.SaveChangesAsync();

        return AccountOperationResult.Success();
    }

    public async Task<AccountOperationResult> VerifyEmailAsync(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return AccountOperationResult.Failure(AccountErrorCode.InvalidCode, "Code is missing.");
        }

        var hashedCode = VerificationCodeHasher.Hash(code);
        var verificationCode = await _dbContext.VerificationCodes
            .FirstOrDefaultAsync(x => x.Code == hashedCode);

        var error = ValidateVerificationCode(verificationCode);
        if (error is not null)
        {
            return AccountOperationResult.Failure(error.Value.Code, error.Value.Message);
        }

        var user = await _userManager.FindByIdAsync(verificationCode!.UserId);
        if (user is null)
        {
            return AccountOperationResult.Failure(AccountErrorCode.NotFound, "User not found.");
        }

        user.EmailConfirmed = true;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return AccountOperationResult.Failure(
                AccountErrorCode.ValidationFailed,
                result.Errors.Select(e => e.Description).ToArray());
        }

        verificationCode.IsActivated = true;
        _dbContext.VerificationCodes.Update(verificationCode);
        await _dbContext.SaveChangesAsync();

        return AccountOperationResult.Success();
    }

    private static (AccountErrorCode Code, string Message)? ValidateVerificationCode(VerificationCode? verificationCode)
    {
        if (verificationCode is null)
        {
            return (AccountErrorCode.InvalidCode, "Invalid verification Code.");
        }

        if (verificationCode.IsActivated)
        {
            return (AccountErrorCode.AlreadyActivated, "This code has already been activated.");
        }

        if (verificationCode.IsExpired)
        {
            return (AccountErrorCode.ExpiredCode, "This code has expired.");
        }

        return null;
    }
}
