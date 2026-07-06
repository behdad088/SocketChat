using Identity.API.Models;
using Identity.API.Services.Account;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Identity.API.Pages.Account.ResetPassword;

[EnableRateLimiting("reset-password")]
public class Index : PageModel
{
    private readonly IAccountService _accountService;

    [BindProperty] public InputModel Input { get; set; } = default!;
    public ViewModel View { get; set; } = default!;

    public Index(IAccountService accountService)
    {
        _accountService = accountService;
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

        var result = await _accountService.ValidateResetCodeAsync(code);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Errors.FirstOrDefault() ?? "Invalid verification Code.");
            BindModel(
                redirectUrl: Request.Query["returnUrl"],
                code: code,
                invalidCode: true);
            return Page();
        }

        BindModel(
            redirectUrl: Request.Query["returnUrl"],
            code: code,
            userId: result.Value);

        return Page();
    }

    public async Task<IActionResult> OnPost()
    {
        if (!ModelState.IsValid)
        {
            BindModel(redirectUrl: Input.ReturnUrl);
            return Page();
        }

        var result = await _accountService.ResetPasswordAsync(Input.UserId!, Input.Code!, Input.Password ?? string.Empty);
        if (!result.Succeeded)
        {
            var errors = string.Join(" ", result.Errors);
            ModelState.AddModelError(string.Empty, errors);
            BindModel(
                redirectUrl: Input.ReturnUrl,
                code: Input.Code,
                userId: Input.UserId,
                invalidCode: false);
            return Page();
        }

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
