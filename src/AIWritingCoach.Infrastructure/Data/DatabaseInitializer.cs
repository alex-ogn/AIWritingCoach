using AIWritingCoach.Core.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AIWritingCoach.Infrastructure.Data;

public static class DatabaseInitializer
{
    /// <summary>Migrates (if enabled), seeds reference data and, in Personal mode, ensures the local user row.</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var options = provider.GetRequiredService<DatabaseOptions>();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));
        await using var db = provider.GetRequiredService<IAppDbContextFactory>().Create();

        if (options.MigrateOnStartup)
        {
            IEnumerable<string> migrations;
            try
            {
                migrations = db.Database.GetMigrations();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Could not load migrations for provider '{options.Provider}'. " +
                    $"Make sure the project '{DatabaseSetup.MigrationsAssembly(options.Provider)}' exists and is referenced by the web app.", ex);
            }

            if (!migrations.Any())
            {
                throw new InvalidOperationException(
                    $"No migrations found for provider '{options.Provider}' in '{DatabaseSetup.MigrationsAssembly(options.Provider)}'.");
            }

            logger.LogInformation("Applying database migrations ({Provider})", options.Provider);
            await db.Database.MigrateAsync(cancellationToken);
        }

        await provider.GetRequiredService<DataSeeder>().SeedAsync(db, cancellationToken);

        if (provider.GetService<PersonalUserOptions>() is { } personal)
        {
            await EnsurePersonalUserAsync(db, personal, provider.GetRequiredService<TimeProvider>(), cancellationToken);
        }
    }

    private static async Task EnsurePersonalUserAsync(AppDbContext db, PersonalUserOptions personal, TimeProvider time, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(u => u.Id == personal.UserId, cancellationToken))
        {
            return;
        }

        db.Users.Add(new AppUser
        {
            Id = personal.UserId,
            UserName = personal.UserName,
            NormalizedUserName = personal.UserName.ToUpperInvariant(),
            DisplayName = personal.DisplayName,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAt = time.GetUtcNow().UtcDateTime,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>The single fixed user of Personal mode.</summary>
public sealed class PersonalUserOptions
{
    public Guid UserId { get; set; } = new("6f1c3a52-0b7e-4c43-9a51-2d1f7f0c0001");

    public string UserName { get; set; } = "local";

    public string DisplayName { get; set; } = "Me";
}

public sealed class PersonalCurrentUser(PersonalUserOptions options) : ICurrentUser
{
    public Guid UserId => options.UserId;

    public bool IsAuthenticated => true;
}
