using Identity.API.Models;
using Microsoft.AspNetCore.Identity;

namespace Identity.API.Tests.IntegrationTests;

[Collection(TestCollection.Name)]
public class UserVersionTests(IdentityApiSpecification specification)
{
    private const string Password = "Pass123$";

    private IServiceScope CreateScope() =>
        specification._factory!.Services.CreateScope();

    private static ApplicationUser NewUser()
    {
        var email = $"version-{Guid.NewGuid():N}@test.com";
        return new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
    }

    private static async Task<ApplicationUser> CreateUserAsync(IServiceScope scope)
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = NewUser();
        (await userManager.CreateAsync(user, Password)).Succeeded.ShouldBeTrue();
        return user;
    }

    private async Task<int> StoredVersionAsync(string userId)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId);
        return stored.Version;
    }

    [Fact]
    public async Task CreatingAUserLeavesVersionAtZero()
    {
        // Arrange
        using var scope = CreateScope();
        
        // Act
        var user = await CreateUserAsync(scope);

        // Assert
        (await StoredVersionAsync(user.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task UpdatingAUserIncrementsVersionEachTime()
    {
        // Arrange
        using var scope = CreateScope();
        
        // Act - Assert
        var user = await CreateUserAsync(scope);
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        user.Name = "First";
        (await userManager.UpdateAsync(user)).Succeeded.ShouldBeTrue();
        (await StoredVersionAsync(user.Id)).ShouldBe(1);

        user.Name = "Second";
        (await userManager.UpdateAsync(user)).Succeeded.ShouldBeTrue();
        (await StoredVersionAsync(user.Id)).ShouldBe(2);
    }

    [Fact]
    public async Task SavingAVerificationCodeDoesNotBumpUserVersion()
    {
        // Arrange
        using var scope = CreateScope();
        
        // Act
        var user = await CreateUserAsync(scope);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.VerificationCodes.Add(new VerificationCode
        {
            UserId = user.Id,
            Code = new string('a', 64),
            Type = "EmailVerification",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Assert
        (await StoredVersionAsync(user.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task StaleVersionUpdateThrowsConcurrencyException()
    {
        // Arrange
        string userId;
        using (var setupScope = CreateScope())
        {
            userId = (await CreateUserAsync(setupScope)).Id;
        }

        using var scopeA = CreateScope();
        using var scopeB = CreateScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var userA = await dbA.Users.SingleAsync(u => u.Id == userId);
        var userB = await dbB.Users.SingleAsync(u => u.Id == userId);

        // Act - Assert
        userA.Name = "Writer A";
        await dbA.SaveChangesAsync(); // DB version: 0 -> 1

        userB.Name = "Writer B"; // still holds original Version 0
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());
    }
}
