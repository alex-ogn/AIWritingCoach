using AIWritingCoach.Core.Users;
using AIWritingCoach.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Design;

namespace AIWritingCoach.Migrations.SqlServer;

/// <summary>
/// Used only by <c>dotnet ef</c>:
/// <code>dotnet ef migrations add &lt;Name&gt; -p src/AIWritingCoach.Migrations.SqlServer -s src/AIWritingCoach.Migrations.SqlServer</code>
/// Override the connection with the <c>AIWRITINGCOACH_SQLSERVER</c> environment variable.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string DefaultConnection =
        "Server=localhost;Database=AIWritingCoach;Trusted_Connection=True;TrustServerCertificate=True";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("AIWRITINGCOACH_SQLSERVER") ?? DefaultConnection;
        return new AppDbContext(DatabaseSetup.BuildOptions(DatabaseProvider.SqlServer, connection), new DesignTimeUser(), TimeProvider.System);
    }

    private sealed class DesignTimeUser : ICurrentUser
    {
        public Guid UserId => Guid.Empty;

        public bool IsAuthenticated => false;
    }
}
