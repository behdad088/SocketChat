using Identity.API.Services.Account;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Identity.API.Pages.Account.EmailVerification;

[EnableRateLimiting("email-verification")]
public class Index : PageModel
{
    private readonly IAccountService _accountService;

    [TempData]
    public string VerificationCode { get; set; } = string.Empty;

    public Index(IAccountService accountService)
    {
        _accountService = accountService;
    }

    public IActionResult OnGet(string code)
    {
        return Page();
    }

    public async Task<JsonResult> OnGetVerifyCode(string code)
    {
        var result = await _accountService.VerifyEmailAsync(code);
        if (!result.Succeeded)
        {
            return new JsonResult(new { success = false, message = result.Errors.FirstOrDefault() ?? "Invalid verification code." });
        }

        return new JsonResult(new { success = true, message = "Your email has been successfully verified!" });
    }
}
