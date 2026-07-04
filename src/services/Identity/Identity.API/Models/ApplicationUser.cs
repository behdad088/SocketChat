using System.ComponentModel.DataAnnotations;
using Identity.API.Data;
using Microsoft.AspNetCore.Identity;

namespace Identity.API.Models;

// Add profile data for application users by adding properties to the ApplicationUser class
public class ApplicationUser : IdentityUser
{
    [MaxLength(2048)]
    public string ProfilePicture { get; set; } = string.Empty;
    [MaxLength(50)]
    public string? Name { get; set; }
    [MaxLength(50)]
    public string? LastName { get; set; }
    
    public ICollection<VerificationCode> EmailVerifications { get; set; } = new List<VerificationCode>();

}