using System.Security.Claims;
using Duende.IdentityModel;
using Identity.API.Models;
using Identity.API.Services.EmailService;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Identity.API.Pages.Account.Register;

[EnableRateLimiting("register")]
public class Index : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IVerificationEmailService _verificationEmailService;

    public Index(
        UserManager<ApplicationUser> userManager,
        IVerificationEmailService verificationEmailService)
    {
        _userManager = userManager;
        _verificationEmailService = verificationEmailService;
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
        if (ModelState.IsValid)
        {
            var dbUser = await _userManager.FindByNameAsync(Input.Email!.ToLowerInvariant());
            
            if (dbUser != null)
            {
                ModelState.AddModelError(string.Empty, "Username already exists.");
                return Page();
            }
            
            var user = new ApplicationUser
            {
                UserName = Input.Email.ToLowerInvariant(),
                Email = Input.Email.ToLowerInvariant(),
                EmailConfirmed = false,
                Name = Input.Email.Split('@')[0], // use the part before @ as name
            };
            
            var result = await _userManager.CreateAsync(user, Input.Password!);

            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty,
                    $"failed to create user due to {string.Join(", ", result.Errors.Select(e => e.Description))}");
                return Page();
            }
            
            result = await _userManager.AddToRoleAsync(user, Config.Roles.Customer);
            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "failed to add user to role.");
                return Page();
            }
            
            result = await AddClaimsAsync(user, [
                new Claim(JwtClaimTypes.Email, Input.Email)
            ]);
            
            if (!result.Succeeded)
            {
                throw new Exception(result.Errors.First().Description);
            }
            
            RegisteredUserId = Guid.Parse(user.Id);
            RegisteredUserEmail = Input.Email.ToLowerInvariant();
            
            await _verificationEmailService.SendEmailAsync(user.Email, user.Id, EmailType.EmailVerification);
            
            RegistrationMessage = "User created successfully. Check your email for verification!";
            return RedirectToPage(null, new { returnUrl = Input.ReturnUrl });
        }
        
        return Page();
    }
    
    public async Task<IActionResult> OnPostResendEmail(string userId, string userEmail)
    {
        if (string.IsNullOrEmpty(userEmail))
        {
            return RedirectToPage();
        }
        
        RegisteredUserEmail = userEmail.ToLowerInvariant();
        RegisteredUserId = Guid.Parse(userId);
        
        await _verificationEmailService.SendEmailAsync(
            userEmail: RegisteredUserEmail!,
            userId: RegisteredUserId!.ToString()!,
            emailType: EmailType.EmailVerification).ConfigureAwait(false);
        
        RegistrationMessage = $"A new confirmation email was sent to {RegisteredUserEmail}.";
        return RedirectToPage(null, new { returnUrl = Input.ReturnUrl });
    }
    
    private async Task<IdentityResult> AddClaimsAsync(
        ApplicationUser user,
        Claim[] claims)
    {
        return await _userManager.AddClaimsAsync(user, claims).ConfigureAwait(false);
    }
}