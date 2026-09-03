using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;

namespace Identity.API.Tests.UnitTests;

public class ApplicationDbContextTests
{
    [Fact]
    public void ApplicationDbContextImplementsIDataProtectionKeyContext()
    {
        typeof(IDataProtectionKeyContext)
            .IsAssignableFrom(typeof(ApplicationDbContext))
            .ShouldBeTrue();
    }
}
