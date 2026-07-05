using Identity.API.Data;
using Identity.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.Pages.Account.ResetPassword;

public class Index : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;
    
    [BindProperty] public InputModel Input { get; set; } = default!;
    public ViewModel View { get; set; } = default!;
    
    public Index(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext)
    {
        _userManager = userManager;
        _dbContext = dbContext;
    }
    
    public async Task<IActionResult> OnGet(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            ModelState.AddModelError(string.Empty, "Code is missing.");
            BindModel(
                redirectUrl: Request.Query["returnUrl"],
                code: code,
                invalidCode: true);
            return Page();
        }
        
        var hashedCode = VerificationCodeHasher.Hash(code);
        var verificationCode = await _dbContext.VerificationCodes
            .FirstOrDefaultAsync(x => x.Code == hashedCode)
            .ConfigureAwait(false);

        var errorMessage = AssertVerificationCode(verificationCode, code);
        if (!string.IsNullOrEmpty(errorMessage))
        {
            ModelState.AddModelError(string.Empty, errorMessage);
            BindModel(
                redirectUrl: Request.Query["returnUrl"],
                code: code,
                invalidCode: true);
            return Page();
        }
        
        BindModel(
            redirectUrl: Request.Query["returnUrl"],
            code: code,
            userId: verificationCode!.UserId);
        
        return Page();
    }
    
    private static string AssertVerificationCode(VerificationCode? verificationCode, string code)
    {
        if (verificationCode == null)
        {
            return "Invalid verification Code.";
        }

        if (verificationCode.IsActivated)
        {
            return "This code has already been activated.";
        }

        if (verificationCode.IsExpired)
        {
            return "This code has expired. Please request a new password reset.";
        }
        
        return string.Empty;
    }
    
    public async Task<IActionResult> OnPost()
    {
        if (!ModelState.IsValid)
        {
            BindModel(redirectUrl: Input.ReturnUrl);
            return Page();
        }
        
        var user = await _userManager.FindByIdAsync(Input.UserId!).ConfigureAwait(false);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "User not found.");
            return Page();
        }
        
        await _userManager.RemovePasswordAsync(user).ConfigureAwait(false);
        var result = await _userManager.AddPasswordAsync(user, Input.Password ?? string.Empty).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            var errors = string.Join(" ", result.Errors.Select(e => e.Description));
            ModelState.AddModelError(string.Empty, errors);
            BindModel(
                redirectUrl: Input.ReturnUrl,
                code: Input.Code,
                userId: Input.UserId,
                invalidCode: false);
            return Page();
        }
        var hashedCode = VerificationCodeHasher.Hash(Input.Code!);
        var verificationCode = await _dbContext.VerificationCodes
            .FirstOrDefaultAsync(x => x.Code == hashedCode)
            .ConfigureAwait(false);
        
        if (verificationCode == null)
        {
            // This should not happen as we already checked the code in OnGet
            // We Should add logging here
            BindModel(
                redirectUrl: Input.ReturnUrl,
                code: Input.Code,
                userId: Input.UserId,
                message: "Your password has been successfully reset.");
            return Page();
        }
        verificationCode.IsActivated = true;
        _dbContext.VerificationCodes.Update(verificationCode);
        await _dbContext.SaveChangesAsync().ConfigureAwait(false);
        BindModel(
            redirectUrl: Input.ReturnUrl,
            code: Input.Code,
            message: "Your password has been successfully reset.",
            userId: Input.UserId);
        
        return Page();
    }
    
    private void BindModel(
        string? redirectUrl = null,
        string? code = null,
        string? email = null,
        string? message = null,
        string? userId = null,
        bool invalidCode = false)
    {
        Input = new InputModel
        {
            ReturnUrl = redirectUrl,
            Code = code,
            UserId = userId
        };
        
        View = new ViewModel
        {
            Message = message,
            Code = code,
            InvalidCode = invalidCode
        };
    }
}