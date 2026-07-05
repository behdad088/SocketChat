using Identity.API.Models;
using Identity.API.Services.EmailService;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Identity.API.Pages.Account.ForgotPassword;

[EnableRateLimiting("forgot-password")]
public class Index : PageModel
{
    private readonly IVerificationEmailService _verificationEmailService;
    private readonly UserManager<ApplicationUser> _userManager;
    
    public Index(
        IVerificationEmailService verificationEmailService,
        UserManager<ApplicationUser> userManager)
    {
        _verificationEmailService = verificationEmailService;
        _userManager = userManager;
    }
    
    [BindProperty] public InputModel Input { get; set; } = default!;
    public ViewModel View { get; set; } = default!;
    
    public IActionResult OnGet()
    {
        BindModel(Request.Query["returnUrl"]);
        
        return Page();
    }
    
    public async Task<IActionResult> OnPost()
    {
        if (!ModelState.IsValid)
        {
            BindModel(Input.ReturnUrl);
            return Page();
        }

        var user = await _userManager.FindByEmailAsync(Input.Email!.ToLowerInvariant());
        if (user == null)
        {
            BindModel(Input.ReturnUrl, Input.Email, true);
            
            return Page();
        }

        await _verificationEmailService.SendEmailAsync(Input.Email, user.Id, EmailType.ForgotPassword, Input.ReturnUrl)
            .ConfigureAwait(false);
        
        BindModel(Input.ReturnUrl, Input.Email, true);
        return Page();
    }

    private void BindModel(
        string? redirectUrl = null,
        string? email = null,
        bool showMessage = false)
    {
        Input = new InputModel
        {
            ReturnUrl = redirectUrl
        };
        
        View = new ViewModel
        {
            Email = email,
            ShowMessage = showMessage
        };
        
    }
}