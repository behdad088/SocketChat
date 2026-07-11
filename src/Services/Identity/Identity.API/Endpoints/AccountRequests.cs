using System.ComponentModel.DataAnnotations;

namespace Identity.API.Endpoints;

public record RegisterRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;
    
    public string DisplayName { get; init; } = string.Empty;

    [Required]
    [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
    public string Password { get; init; } = string.Empty;

    [Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
    public string? ConfirmPassword { get; init; }
}

public record ResendVerificationEmailRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;
}

public record ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    public string? ReturnUrl { get; init; }
}

public record ResetPasswordRequest
{
    [Required]
    public string UserId { get; init; } = string.Empty;

    [Required]
    public string Code { get; init; } = string.Empty;

    [Required]
    [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
    public string Password { get; init; } = string.Empty;

    [Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
    public string? ConfirmPassword { get; init; }
}

public record VerifyEmailRequest
{
    [Required]
    public string Code { get; init; } = string.Empty;
}

public record UpdateProfileRequest
{
    [MaxLength(50)]
    public string? Name { get; init; }

    [MaxLength(50)]
    public string? LastName { get; init; }

    [Required(AllowEmptyStrings = true)]
    [MaxLength(2048)]
    public string ProfilePicture { get; init; } = string.Empty;
}
