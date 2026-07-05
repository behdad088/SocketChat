using Duende.IdentityServer;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Identity.API.Models;
using Identity.API.Services.EmailService;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Identity.API.Pages.Account.Login;

[SecurityHeaders]
[AllowAnonymous]
[EnableRateLimiting("login")]
public class Index : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IIdentityServerInteractionService _interaction;
    private readonly IEventService _events;
    private readonly IAuthenticationSchemeProvider _schemeProvider;
    private readonly IIdentityProviderStore _identityProviderStore;
    private readonly IVerificationEmailService _verificationEmailService;
    
    public ViewModel View { get; set; } = default!;

    [BindProperty] public InputModel Input { get; set; } = default!;

    public Index(
        IIdentityServerInteractionService interaction,
        IAuthenticationSchemeProvider schemeProvider,
        IIdentityProviderStore identityProviderStore,
        IEventService events,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IVerificationEmailService verificationEmailService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _interaction = interaction;
        _schemeProvider = schemeProvider;
        _identityProviderStore = identityProviderStore;
        _events = events;
        _verificationEmailService = verificationEmailService;
    }

    public async Task<IActionResult> OnGet(string? returnUrl)
    {
        await BuildModelAsync(returnUrl);

        if (View.IsExternalLoginOnly)
        {
            // we only have one option for logging in and it's an external provider
            return RedirectToPage("/ExternalLogin/Challenge", new { scheme = View.ExternalLoginScheme, returnUrl });
        }

        return Page();
    }

    public async Task<IActionResult> OnPost()
    {
        // check if we are in the context of an authorization request
        var context = await _interaction.GetAuthorizationContextAsync(Input.ReturnUrl);

        // the user clicked the "cancel" button
        if (Input.Button != "login")
        {
            if (context != null)
            {
                // This "can't happen", because if the ReturnUrl was null, then the context would be null
                ArgumentNullException.ThrowIfNull(Input.ReturnUrl, nameof(Input.ReturnUrl));

                // if the user cancels, send a result back into IdentityServer as if they 
                // denied the consent (even if this client does not require consent).
                // this will send back an access denied OIDC error response to the client.
                await _interaction.DenyAuthorizationAsync(context, AuthorizationError.AccessDenied);

                // we can trust model.ReturnUrl since GetAuthorizationContextAsync returned non-null
                if (context.IsNativeClient())
                {
                    // The client is native, so this change in how to
                    // return the response is for better UX for the end user.
                    return this.LoadingPage(Input.ReturnUrl);
                }

                return Redirect(Input.ReturnUrl ?? "~/");
            }
            else
            {
                // since we don't have a valid context, then we just go back to the home page
                return Redirect("~/");
            }
        }

        if (ModelState.IsValid)
        {
            var user = await _userManager.FindByNameAsync(Input.Username!);

            if (user == null)
            {
                await RaiseLoginFailureEvent(
                    username: Input.Username!,
                    error: "Invalid credentials",
                    errorMessage: LoginOptions.InvalidCredentialsErrorMessage,
                    clientId: context?.Client.ClientId);
                
                await BuildModelAsync(Input.ReturnUrl);
                return Page();
            }
            
            var isUserValid =
                await _signInManager.CheckPasswordSignInAsync(user: user, password: Input.Password!,
                    lockoutOnFailure: true);

            if (!isUserValid.Succeeded)
            {
                await RaiseLoginFailureEvent(
                    username: Input.Username!,
                    error: "Invalid credentials",
                    errorMessage: LoginOptions.InvalidCredentialsErrorMessage,
                    clientId: context?.Client.ClientId);
                
                await BuildModelAsync(Input.ReturnUrl);
                return Page();
            }
            
            if (!user.EmailConfirmed)
            {
                await RaiseLoginFailureEvent(
                    username: Input.Username!,
                    error: "email not verified",
                    errorMessage: LoginOptions.EmailNotVerifiedErrorMessage,
                    clientId: context?.Client.ClientId);
                    
                await BuildModelAsync(Input.ReturnUrl, user.Id, user.Email);
                return Page();
            }
            
            // Only remember login if allowed
            var rememberLogin = LoginOptions.AllowRememberLogin && Input.RememberLogin;
            var result = await _signInManager.PasswordSignInAsync(Input.Username!, Input.Password!,
                isPersistent: rememberLogin, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                await _events.RaiseAsync(new UserLoginSuccessEvent(user!.UserName, user.Id, user.UserName,
                    clientId: context?.Client.ClientId));
                
                Telemetry.Metrics.UserLogin(context?.Client.ClientId, IdentityServerConstants.LocalIdentityProvider);

                if (context != null)
                {
                    // This "can't happen", because if the ReturnUrl was null, then the context would be null
                    ArgumentNullException.ThrowIfNull(Input.ReturnUrl, nameof(Input.ReturnUrl));

                    if (context.IsNativeClient())
                    {
                        // The client is native, so this change in how to
                        // return the response is for better UX for the end user.
                        return this.LoadingPage(Input.ReturnUrl);
                    }

                    // we can trust model.ReturnUrl since GetAuthorizationContextAsync returned non-null
                    return Redirect(Input.ReturnUrl ?? "~/");
                }

                // request for a local page
                if (Url.IsLocalUrl(Input.ReturnUrl))
                {
                    return Redirect(Input.ReturnUrl);
                }
                else if (string.IsNullOrEmpty(Input.ReturnUrl))
                {
                    return Redirect("~/");
                }
                else
                {
                    // user might have clicked on a malicious link - should be logged
                    throw new ArgumentException("invalid return URL");
                }
            }
            
            await RaiseLoginFailureEvent(
                username: Input.Username!,
                error: "invalid credentials",
                errorMessage: LoginOptions.InvalidCredentialsErrorMessage,
                clientId: context?.Client.ClientId);
        }

        // something went wrong, show form with error
        await BuildModelAsync(Input.ReturnUrl);
        return Page();
    }
    
    public async Task<IActionResult> OnPostResendEmail(string userId, string userEmail)
    {
        ModelState.Clear();

        if (string.IsNullOrEmpty(userEmail))
        {
            return RedirectToPage();
        }
        
        await _verificationEmailService.SendEmailAsync(
            userEmail: userEmail,
            userId: userId,
            emailType: EmailType.EmailVerification).ConfigureAwait(false);
        
        var emailVerificationMessage = $"A new confirmation email was sent to {userEmail}.";

        await BuildModelAsync(Input.ReturnUrl, emailVerificationMessage: emailVerificationMessage);
        return Page();
    }

    
    private async Task RaiseLoginFailureEvent(
        string username,
        string error,
        string errorMessage,
        string? clientId)
    {
        
        await _events.RaiseAsync(new UserLoginFailureEvent(username, error,
            clientId: clientId));
        Telemetry.Metrics.UserLoginFailure(clientId, IdentityServerConstants.LocalIdentityProvider,
            error);
        ModelState.AddModelError(string.Empty, errorMessage);
    }

    private async Task BuildModelAsync(
        string? returnUrl,
        string? userId = null,
        string? email = null,
        string? emailVerificationMessage = null)
    {

        var modelStateErrors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
        foreach (var modelStateValue in modelStateErrors)
        {
            ModelState.AddModelError(string.Empty, modelStateValue);
        }
        
        Input = new InputModel
        {
            ReturnUrl = returnUrl
        };

        var context = await _interaction.GetAuthorizationContextAsync(returnUrl);
        if (context?.IdP != null)
        {
            var scheme = await _schemeProvider.GetSchemeAsync(context.IdP);
            if (scheme != null)
            {
                var local = context.IdP == Duende.IdentityServer.IdentityServerConstants.LocalIdentityProvider;

                // this is meant to short circuit the UI and only trigger the one external IdP
                View = new ViewModel
                {
                    EnableLocalLogin = local,
                };

                Input.Username = context.LoginHint;

                if (!local)
                {
                    View.ExternalProviders =
                    [
                        new ViewModel.ExternalProvider(authenticationScheme: context.IdP,
                            displayName: scheme.DisplayName)
                    ];
                }
            }

            return;
        }

        var schemes = await _schemeProvider.GetAllSchemesAsync();

        var providers = schemes
            .Where(x => x.DisplayName != null)
            .Select(x => new ViewModel.ExternalProvider
            (
                authenticationScheme: x.Name,
                displayName: x.DisplayName ?? x.Name
            )).ToList();

        var dynamicSchemes = (await _identityProviderStore.GetAllSchemeNamesAsync())
            .Where(x => x.Enabled)
            .Select(x => new ViewModel.ExternalProvider
            (
                authenticationScheme: x.Scheme,
                displayName: x.DisplayName ?? x.Scheme
            ));
        providers.AddRange(dynamicSchemes);


        var allowLocal = true;
        var client = context?.Client;
        if (client != null)
        {
            allowLocal = client.EnableLocalLogin;
            if (client.IdentityProviderRestrictions != null && client.IdentityProviderRestrictions.Count != 0)
            {
                providers = providers.Where(provider =>
                    client.IdentityProviderRestrictions.Contains(provider.AuthenticationScheme)).ToList();
            }
        }

        View = new ViewModel
        {
            AllowRememberLogin = LoginOptions.AllowRememberLogin,
            EnableLocalLogin = allowLocal && LoginOptions.AllowLocalLogin,
            ExternalProviders = providers.ToArray(),
            
        };
        
        if (email != null && userId != null)
        {
            View.SendVerificationCode = new ViewModel.SendVerificationCodeViewModel
            {
                UserId = userId,
                Email = email
            };
        }
        else if (userId != null)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                View.SendVerificationCode = new ViewModel.SendVerificationCodeViewModel
                {
                    UserId = user.Id,
                    Email = user.Email
                };
            }
        }
        
        if (!string.IsNullOrEmpty(emailVerificationMessage))
        {
            View.ShowSendVerificationMessage = emailVerificationMessage;
        }
    }
}