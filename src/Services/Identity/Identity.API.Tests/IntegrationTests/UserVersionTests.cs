using Identity.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.Tests.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
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
    public async Task Creating_a_user_leaves_version_at_zero()
    {
        using var scope = CreateScope();
        var user = await CreateUserAsync(scope);

        (await StoredVersionAsync(user.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task Updating_a_user_increments_version_each_time()
    {
        using var scope = CreateScope();
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
    public async Task Saving_a_verification_code_does_not_bump_user_version()
    {
        using var scope = CreateScope();
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

        (await StoredVersionAsync(user.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task Stale_version_update_throws_concurrency_exception()
    {
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

        userA.Name = "Writer A";
        await dbA.SaveChangesAsync(); // DB version: 0 -> 1

        userB.Name = "Writer B"; // still holds original Version 0
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());
    }
}
