using Identity.API.Messaging.Outbox;
using Identity.API.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Identity.API.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }
    public DbSet<VerificationCode> VerificationCodes { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>()
            .Property(u => u.Version)
            .IsConcurrencyToken();

        builder.Entity<VerificationCode>()
            .HasOne(cc => cc.User)
            .WithMany(x => x.EmailVerifications)
            .HasForeignKey(cc => cc.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<OutboxMessage>(entity =>
        {
            entity.Property(m => m.EventType).HasMaxLength(256);
            entity.Property(m => m.Payload).HasColumnType("jsonb");
            entity.Property(m => m.TraceParent).HasMaxLength(128);
            // creating a partial index: the dispatcher only ever scans pending rows.
            entity.HasIndex(m => m.NextAttemptAt).HasFilter("\"DispatchedAt\" IS NULL");
            entity.HasIndex(m => m.DispatchedAt);
        });

        base.OnModelCreating(builder);
    }

    // Only the two (bool, ...) overloads are overridden: the parameterless
    // SaveChanges/SaveChangesAsync delegate to them virtually, so overriding
    // all four would increment twice per save.
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        IncrementUserVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        IncrementUserVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void IncrementUserVersions()
    {
        foreach (var entry in ChangeTracker.Entries<ApplicationUser>())
        {
            if (entry.State == EntityState.Modified && HasBusinessChanges(entry))
            {
                entry.Entity.Version++;
            }
        }
    }

    // ASP.NET Core Identity's UserManager routes AddToRoleAsync, AddClaimsAsync, and similar
    // role/claim membership calls through IUserStore.UpdateAsync, which regenerates
    // ConcurrencyStamp and saves the user even though no profile-visible field changed.
    // That alone flips the entry to EntityState.Modified, so gating solely on entry.State
    // (as before) inflates Version on every such call. Only bump Version when a field a
    // client can actually observe (and that our outbound UserCreated/UserUpdated events
    // carry) has a genuinely different value than what's persisted.
    private static readonly string[] VersionedProperties =
    [
        nameof(ApplicationUser.Name),
        nameof(ApplicationUser.LastName),
        nameof(ApplicationUser.ProfilePicture),
        nameof(ApplicationUser.Email),
        nameof(ApplicationUser.UserName),
        nameof(ApplicationUser.EmailConfirmed)
    ];

    private static bool HasBusinessChanges(EntityEntry<ApplicationUser> entry)
    {
        foreach (var propertyName in VersionedProperties)
        {
            var property = entry.Property(propertyName);
            if (property.IsModified && !Equals(property.OriginalValue, property.CurrentValue))
            {
                return true;
            }
        }

        return false;
    }
}
