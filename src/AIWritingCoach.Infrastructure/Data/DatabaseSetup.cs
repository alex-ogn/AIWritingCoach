using AIWritingCoach.Core.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIWritingCoach.Infrastructure.Data;

public enum DatabaseProvider
{
    SqlServer,
    PostgreSql,
    Sqlite,
}

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public DatabaseProvider Provider { get; set; } = DatabaseProvider.SqlServer;

    /// <summary>Apply pending migrations at startup (convenient for Personal mode).</summary>
    public bool MigrateOnStartup { get; set; } = true;
}

/// <summary>Creates short-lived contexts (Blazor Server circuits are long-lived, contexts must not be).</summary>
public interface IAppDbContextFactory
{
    AppDbContext Create();
}

internal sealed class AppDbContextFactory(DbContextOptions<AppDbContext> options, ICurrentUser currentUser, TimeProvider timeProvider)
    : IAppDbContextFactory
{
    public AppDbContext Create() => new(options, currentUser, timeProvider);
}

public static class DatabaseSetup
{
    /// <summary>Each provider has its own migrations assembly so schemas can evolve independently.</summary>
    public static string MigrationsAssembly(DatabaseProvider provider) => $"AIWritingCoach.Migrations.{provider}";

    public static DbContextOptions<AppDbContext> BuildOptions(DatabaseProvider provider, string connectionString)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        var migrations = MigrationsAssembly(provider);

        _ = provider switch
        {
            DatabaseProvider.SqlServer => builder.UseSqlServer(connectionString, o => o.MigrationsAssembly(migrations)),
            DatabaseProvider.PostgreSql => builder.UseNpgsql(connectionString, o => o.MigrationsAssembly(migrations)),
            DatabaseProvider.Sqlite => builder.UseSqlite(connectionString, o => o.MigrationsAssembly(migrations)),
            _ => throw new NotSupportedException($"Unknown database provider '{provider}'."),
        };

        // Child rows (errors, scores) are only reached through their filtered Review, so this warning is expected.
        builder.ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
        return builder.Options;
    }

    public static IServiceCollection AddAppDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
        var connectionString = configuration.GetConnectionString(options.Provider.ToString());
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Missing connection string 'ConnectionStrings:{options.Provider}' for database provider '{options.Provider}'.");
        }

        services.AddSingleton(options);
        services.AddSingleton(BuildOptions(options.Provider, connectionString));
        services.AddScoped<IAppDbContextFactory, AppDbContextFactory>();
        services.AddScoped(sp => sp.GetRequiredService<IAppDbContextFactory>().Create());
        return services;
    }
}
