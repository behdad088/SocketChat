using System.ComponentModel.DataAnnotations;

namespace Identity.API.Pages.Account.ForgotPassword;

public class InputModel
{
    public string? ReturnUrl { get; set; }
    [Required]
    public string? Email { get; set; }
}