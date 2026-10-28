using AIWritingCoach.Core.Users;
using AIWritingCoach.Core.Writing;
using AIWritingCoach.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace AIWritingCoach.Tests.Data;

/// <summary>Runs the real model against in-memory SQLite (provider-agnostic mappings, filters, seeding).</summary>
public sealed class AppDbContextTests : IDisposable
{
    private static readonly Guid Alice = Guid.NewGuid();
    private static readonly Guid Bob = Guid.NewGuid();

    private readonly SqliteConnection _keepAlive;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));

    public AppDbContextTests()
    {
        var connectionString = $"Data Source=test-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _keepAlive = new SqliteConnection(connectionString);
        _keepAlive.Open();
        _options = DatabaseSetup.BuildOptions(DatabaseProvider.Sqlite, connectionString);
        using var db = Context(Alice);
        db.Database.EnsureCreated();
    }

    public void Dispose() => _keepAlive.Dispose();

    private AppDbContext Context(Guid userId) => new(_options, new TestUser(userId), _time);

    [Fact]
    public async Task Query_filters_isolate_users_and_ownership_is_stamped()
    {
        await using (var db = Context(Alice))
        {
            db.Documents.Add(new WritingDocument { Title = "Alice's essay", Content = "x" });
            await db.SaveChangesAsync();
        }

        await using (var db = Context(Bob))
        {
            Assert.Empty(await db.Documents.ToListAsync());
        }

        await using (var db = Context(Alice))
        {
            var document = Assert.Single(await db.Documents.ToListAsync());
            Assert.Equal(Alice, document.UserId);
        }
    }

    [Fact]
    public async Task Saving_another_users_entity_is_rejected()
    {
        await using var db = Context(Alice);
        db.Documents.Add(new WritingDocument { UserId = Bob, Title = "Not mine" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Timestamps_are_utc_and_created_at_is_preserved()
    {
        var document = new WritingDocument { Title = "T" };
        await using (var db = Context(Alice))
        {
            db.Documents.Add(document);
            await db.SaveChangesAsync();
        }

        _time.Advance(TimeSpan.FromHours(1));
        await using (var db = Context(Alice))
        {
            document.Title = "T2";
            db.Documents.Update(document);
            await db.SaveChangesAsync();
        }

        await using (var db = Context(Alice))
        {
            var stored = await db.Documents.SingleAsync();
            Assert.Equal(DateTimeKind.Utc, stored.CreatedAt.Kind);
            Assert.Equal(new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc), stored.CreatedAt);
            Assert.Equal(new DateTime(2026, 10, 1, 13, 0, 0, DateTimeKind.Utc), stored.UpdatedAt);
        }
    }

    [Fact]
    public async Task Seeder_is_idempotent_and_system_data_is_visible_to_every_user()
    {
        var seeder = new DataSeeder(NullLogger<DataSeeder>.Instance);
        await using (var db = Context(Alice))
        {
            await seeder.SeedAsync(db);
            await seeder.SeedAsync(db);
        }

        await using (var db = Context(Bob))
        {
            var rubrics = await db.Rubrics.ToListAsync();
            Assert.Equal(3, rubrics.Count);
            Assert.All(rubrics, r => Assert.NotEmpty(r.Criteria));
            Assert.Equal(7, await db.Profiles.CountAsync());
            Assert.Equal(1, await db.PromptTemplates.CountAsync());

            var b2Essay = await db.Profiles.SingleAsync(p => p.Key == "b2-essay");
            Assert.Equal(4, b2Essay.CriterionKeys.Count);
            Assert.Equal(CefrLevel.B2, b2Essay.TargetLevel);
        }
    }

    private sealed class TestUser(Guid id) : ICurrentUser
    {
        public Guid UserId => id;

        public bool IsAuthenticated => true;
    }
}
