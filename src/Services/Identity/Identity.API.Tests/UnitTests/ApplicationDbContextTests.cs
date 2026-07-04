using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;

namespace Identity.API.Tests.UnitTests;

public class ApplicationDbContextTests
{
    [Fact]
    public void ApplicationDbContext_implements_IDataProtectionKeyContext()
    {
        typeof(IDataProtectionKeyContext)
            .IsAssignableFrom(typeof(ApplicationDbContext))
            .ShouldBeTrue();
    }
}
