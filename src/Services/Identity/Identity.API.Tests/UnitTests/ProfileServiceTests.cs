using System.Security.Claims;
using Duende.IdentityServer.Models;
using Identity.API.Models;
using Identity.API.Services;
using Microsoft.AspNetCore.Identity;
using NSubstitute;

namespace Identity.API.Tests.UnitTests;

public class ProfileServiceTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ProfileService _sut;

    public ProfileServiceTests()
    {
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(),
            null, null, null, null, null, null, null, null);

        _roleManager = Substitute.For<RoleManager<IdentityRole>>(
            Substitute.For<IRoleStore<IdentityRole>>(),
            null, null, null, null);

        _sut = new ProfileService(_userManager, _roleManager, Substitute.For<Microsoft.AspNetCore.Http.IHttpContextAccessor>());
    }

    [Fact]
    public async Task GetProfileDataAsyncIncludesEmailClaimWhenEmailIsSet()
    {
        // Arrange
        var user = BuildUser(email: "alice@example.com", emailConfirmed: true);
        SetupUserManagerReturns(user);
        _userManager.SupportsUserEmail.Returns(true);
        _userManager.SupportsUserRole.Returns(false);
        _userManager.GetRolesAsync(user).Returns(["customer"]);

        var context = BuildContext(user.Id);
        
        // Act
        await _sut.GetProfileDataAsync(context);

        // Assert
        context.IssuedClaims.ShouldContain(c =>
            c.Type == "email" && c.Value == "alice@example.com");
    }

    [Fact]
    public async Task GetProfileDataAsyncIncludesEmailVerifiedTrueWhenConfirmed()
    {
        // Arrange
        var user = BuildUser(email: "alice@example.com", emailConfirmed: true);
        SetupUserManagerReturns(user);
        _userManager.SupportsUserEmail.Returns(true);
        _userManager.SupportsUserRole.Returns(false);
        _userManager.GetRolesAsync(user).Returns([]);

        var context = BuildContext(user.Id);
        
        // Act
        await _sut.GetProfileDataAsync(context);

        // Assert
        context.IssuedClaims.ShouldContain(c =>
            c.Type == "email_verified" && c.Value == "true");
    }

    [Fact]
    public async Task GetProfileDataAsyncIncludesEmailVerifiedFalseWhenNotConfirmed()
    {
        // Arrange
        var user = BuildUser(email: "alice@example.com", emailConfirmed: false);
        SetupUserManagerReturns(user);
        _userManager.SupportsUserEmail.Returns(true);
        _userManager.SupportsUserRole.Returns(false);
        _userManager.GetRolesAsync(user).Returns([]);
        
        var context = BuildContext(user.Id);
        
        // Act
        await _sut.GetProfileDataAsync(context);

        // Assert
        context.IssuedClaims.ShouldContain(c =>
            c.Type == "email_verified" && c.Value == "false");
    }

    [Fact]
    public async Task GetProfileDataAsyncIncludesRoleClaimforEachRole()
    {
        // Arrange
        var user = BuildUser();
        SetupUserManagerReturns(user);
        _userManager.SupportsUserRole.Returns(true);
        _userManager.GetRolesAsync(user).Returns(["customer"]);

        var role = new IdentityRole("customer") { Id = Guid.NewGuid().ToString() };
        _roleManager.FindByNameAsync("customer").Returns(role);
        _roleManager.GetClaimsAsync(role).Returns([]);
        
        var context = BuildContext(user.Id);
        
        // Act
        await _sut.GetProfileDataAsync(context);

        // Assert
        context.IssuedClaims.ShouldContain(c => c.Type == "role" && c.Value == "customer");
    }

    [Fact]
    public async Task GetProfileDataAsyncIncludesPermissionsFromRoleClaims()
    {
        // Arrange
        var user = BuildUser();
        SetupUserManagerReturns(user);
        _userManager.SupportsUserRole.Returns(true);
        _userManager.GetRolesAsync(user).Returns(["customer"]);

        var role = new IdentityRole("customer") { Id = Guid.NewGuid().ToString() };
        var permClaim = new Claim("permissions", "basket:user-basket:checkout");
        _roleManager.FindByNameAsync("customer").Returns(role);
        _roleManager.GetClaimsAsync(role).Returns([permClaim]);

        var context = BuildContext(user.Id);
        
        // Act
        await _sut.GetProfileDataAsync(context);

        // Assert
        context.IssuedClaims.ShouldContain(c =>
            c.Type == "permissions" && c.Value == "basket:user-basket:checkout");
    }

    [Fact]
    public async Task GetProfileDataAsyncThrowsWhenUserNotFound()
    {
        // Arrange
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);

        // Act
        var context = BuildContext("nonexistent-id");
        
        // Assert
        await Should.ThrowAsync<ArgumentException>(() => _sut.GetProfileDataAsync(context));
    }

    [Fact]
    public async Task IsActiveAsyncReturnsTrueForActiveUserWithoutLockout()
    {
        // Arrange
        var user = BuildUser();
        user.LockoutEnabled = false;
        SetupUserManagerReturns(user);
        _userManager.SupportsUserSecurityStamp.Returns(false);

        var context = new IsActiveContext(
            BuildPrincipal(user.Id), new Client(), "test");
        
        // Act
        await _sut.IsActiveAsync(context);

        // Assert
        context.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task IsActiveAsync_returns_false_when_user_not_found()
    {
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);

        var context = new IsActiveContext(
            BuildPrincipal("nonexistent-id"), new Client(), "test");
        await _sut.IsActiveAsync(context);

        context.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task IsActiveAsyncReturnsFalseWhenLockoutEndIsInTheFuture()
    {
        // Arrange
        var user = BuildUser();
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.UtcNow.AddHours(1);
        SetupUserManagerReturns(user);
        _userManager.SupportsUserSecurityStamp.Returns(false);

        var context = new IsActiveContext(
            BuildPrincipal(user.Id), new Client(), "test");
        
        // Act
        await _sut.IsActiveAsync(context);

        // Assert
        context.IsActive.ShouldBeFalse();
    }

    private static ApplicationUser BuildUser(
        string email = "test@example.com",
        bool emailConfirmed = true) => new()
    {
        Id = Guid.NewGuid().ToString(),
        UserName = email,
        Email = email,
        EmailConfirmed = emailConfirmed
    };

    private void SetupUserManagerReturns(ApplicationUser user)
    {
        _userManager.FindByIdAsync(user.Id).Returns(user);
        _userManager.SupportsUserEmail.Returns(false);
        _userManager.SupportsUserPhoneNumber.Returns(false);
        _userManager.SupportsUserRole.Returns(false);
    }

    private static ProfileDataRequestContext BuildContext(string subjectId)
    {
        var principal = BuildPrincipal(subjectId);
        return new ProfileDataRequestContext(principal, new Client(), "caller", []);
    }

    private static ClaimsPrincipal BuildPrincipal(string subjectId) =>
        new(new ClaimsIdentity(
        [
            new Claim("sub", subjectId)
        ], "test"));
}
