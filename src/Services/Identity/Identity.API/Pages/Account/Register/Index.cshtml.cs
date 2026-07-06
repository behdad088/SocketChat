using Identity.API.Services.Account;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Identity.API.Pages.Account.Register;

[EnableRateLimiting("register")]
public class Index : PageModel
{
    private readonly IAccountService _accountService;

    public Index(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [BindProperty]
    public RegisterViewModel Input { get; set; }

    [TempData]
    public string RegistrationMessage { get; set; } = string.Empty; // used to display messages on the page

    [TempData]
    public string? RegisteredUserEmail { get; set; }

    [TempData]
    public Guid? RegisteredUserId { get; set; }

    public async Task<IActionResult> OnGet()
    {
        Input = new RegisterViewModel()
        {
            ReturnUrl = Request.Query["returnUrl"]
        };

        if (TempData["RegistrationMessage"] is string message)
        {
            RegistrationMessage = message;
        }
        await Task.CompletedTask;

        return Page();
    }

    public async Task<IActionResult> OnPost()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _accountService.RegisterAsync(Input.Email!, Input.Password!);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty,
                result.ErrorCode == AccountErrorCode.Conflict
                    ? "Username already exists."
                    : $"failed to create user due to {string.Join(", ", result.Errors)}");
            return Page();
        }

        RegisteredUserId = Guid.Parse(result.Value!.UserId);
        RegisteredUserEmail = result.Value.Email;

        RegistrationMessage = "User created successfully. Check your email for verification!";
        return RedirectToPage(null, new { returnUrl = Input.ReturnUrl });
    }

    public async Task<IActionResult> OnPostResendEmail(string userId, string userEmail)
    {
        if (string.IsNullOrEmpty(userEmail))
        {
            return RedirectToPage();
        }

        RegisteredUserEmail = userEmail.ToLowerInvariant();
        RegisteredUserId = Guid.Parse(userId);

        await _accountService.ResendVerificationEmailAsync(RegisteredUserEmail!);

        RegistrationMessage = $"A new confirmation email was sent to {RegisteredUserEmail}.";
        return RedirectToPage(null, new { returnUrl = Input.ReturnUrl });
    }
}
