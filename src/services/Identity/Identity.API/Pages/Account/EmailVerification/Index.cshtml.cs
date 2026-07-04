using Identity.API.Data;
using Identity.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.Pages.Account.EmailVerification;

public class Index : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;

    [TempData]
    public string VerificationCode { get; set; } = string.Empty;
    
    public Index(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext)
    {
        _userManager = userManager;
        _dbContext = dbContext;
    }
    
    public IActionResult OnGet(string code)
    {
        return Page();
    }
    
    public async Task<JsonResult> OnGetVerifyCode(string code)
    {
        if (string.IsNullOrEmpty(code))
            return new JsonResult(new { success = false, message = "Code is missing." });

        var hashedCode = VerificationCodeHasher.Hash(code);
        var verificationCode = await _dbContext.VerificationCodes
            .FirstOrDefaultAsync(x => x.Code == hashedCode)
            .ConfigureAwait(false);

        if (verificationCode == null)
            return new JsonResult(new { success = false, message = "Invalid verification code." });

        if (verificationCode.IsActivated)
            return new JsonResult(new { success = false, message = "This code has already been activated." });

        if (verificationCode.IsExpired)
            return new JsonResult(new { success = false, message = "This code has expired." });

        var user = await _userManager.FindByIdAsync(verificationCode.UserId ?? string.Empty).ConfigureAwait(false);
        if (user == null)
            return new JsonResult(new { success = false, message = "User not found." });

        user.EmailConfirmed = true;
        var result = await _userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            var errors = string.Join(" ", result.Errors.Select(e => e.Description));
            return new JsonResult(new { success = false, message = errors });
        }

        verificationCode.IsActivated = true;
        _dbContext.VerificationCodes.Update(verificationCode);
        await _dbContext.SaveChangesAsync().ConfigureAwait(false);

        return new JsonResult(new { success = true, message = "Your email has been successfully verified!" });
    }
}