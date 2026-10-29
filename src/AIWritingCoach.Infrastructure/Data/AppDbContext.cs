using AIWritingCoach.Core.Common;
using AIWritingCoach.Core.Reviews;
using AIWritingCoach.Core.Rubrics;
using AIWritingCoach.Core.Usage;
using AIWritingCoach.Core.Users;
using AIWritingCoach.Core.Vocabulary;
using AIWritingCoach.Core.Writing;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AIWritingCoach.Infrastructure.Data;

/// <summary>Application user. Identity tables exist from day one so MultiUser mode needs no schema rewrite.</summary>
public class AppUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Provider-agnostic context. User-owned data is isolated by global query filters on
/// <see cref="ICurrentUser"/>; timestamps and ownership are stamped in <see cref="SaveChangesAsync(bool, CancellationToken)"/>.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser, TimeProvider timeProvider)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    // Referenced by the query filters; EF parameterizes it per context instance.
    private Guid CurrentUserId => currentUser.UserId;

    public DbSet<WritingDocument> Documents => Set<WritingDocument>();

    public DbSet<Review> Reviews => Set<Review>();

    public DbSet<ReviewError> ReviewErrors => Set<ReviewError>();

    public DbSet<CriterionScore> CriterionScores => Set<CriterionScore>();

    public DbSet<VocabularySuggestion> VocabularySuggestions => Set<VocabularySuggestion>();

    public DbSet<Rubric> Rubrics => Set<Rubric>();

    public DbSet<Profile> Profiles => Set<Profile>();

    public DbSet<PromptTemplate> PromptTemplates => Set<PromptTemplate>();

    public DbSet<VocabularyItem> VocabularyItems => Set<VocabularyItem>();

    public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        builder.Entity<WritingDocument>().HasQueryFilter(e => e.UserId == CurrentUserId);
        builder.Entity<Review>().HasQueryFilter(e => e.UserId == CurrentUserId);
        builder.Entity<VocabularyItem>().HasQueryFilter(e => e.UserId == CurrentUserId);
        builder.Entity<AiUsageLog>().HasQueryFilter(e => e.UserId == CurrentUserId);
        builder.Entity<Rubric>().HasQueryFilter(e => e.UserId == null || e.UserId == CurrentUserId);
        builder.Entity<Profile>().HasQueryFilter(e => e.UserId == null || e.UserId == CurrentUserId);
        builder.Entity<PromptTemplate>().HasQueryFilter(e => e.UserId == null || e.UserId == CurrentUserId);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        builder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
        builder.Properties<decimal>().HavePrecision(18, 8);

        // Enums as strings: readable data and safe reordering of enum members.
        builder.Properties<WritingTaskType>().HaveConversion<string>().HaveMaxLength(32);
        builder.Properties<CefrLevel>().HaveConversion<string>().HaveMaxLength(8);
        builder.Properties<ErrorSeverity>().HaveConversion<string>().HaveMaxLength(16);
        builder.Properties<RubricKind>().HaveConversion<string>().HaveMaxLength(16);
        builder.Properties<VocabularyStatus>().HaveConversion<string>().HaveMaxLength(16);
        builder.Properties<VocabularySource>().HaveConversion<string>().HaveMaxLength(16);
        builder.Properties<AiCallStatus>().HaveConversion<string>().HaveMaxLength(16);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampEntities();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampEntities();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void StampEntities()
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is Entity entity)
            {
                if (entry.State == EntityState.Added)
                {
                    if (entity.CreatedAt == default)
                    {
                        entity.CreatedAt = now;
                    }

                    entity.UpdatedAt = now;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entity.UpdatedAt = now;
                    entry.Property(nameof(Entity.CreatedAt)).IsModified = false;
                }
            }

            if (entry.Entity is IUserOwned owned && entry.State is EntityState.Added or EntityState.Modified)
            {
                if (owned.UserId == Guid.Empty)
                {
                    owned.UserId = currentUser.UserId;
                }
                else if (owned.UserId != currentUser.UserId)
                {
                    throw new InvalidOperationException($"Cannot save {entry.Entity.GetType().Name} owned by another user.");
                }
            }
        }
    }
}
