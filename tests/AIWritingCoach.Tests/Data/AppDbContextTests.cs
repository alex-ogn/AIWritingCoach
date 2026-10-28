using AIWritingCoach.Core.Rubrics;
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
            Assert.Equal(6, rubrics.Count);
            Assert.All(rubrics, r => Assert.NotEmpty(r.Criteria));
            Assert.Equal(13, await db.Profiles.CountAsync());
            Assert.Equal(1, await db.PromptTemplates.CountAsync());

            var b2Essay = await db.Profiles.SingleAsync(p => p.Key == "b2-essay");
            Assert.Equal(4, b2Essay.CriterionKeys.Count);
            Assert.Equal(CefrLevel.B2, b2Essay.TargetLevel);

            var c1Report = await db.Profiles.SingleAsync(p => p.Key == "c1-report");
            Assert.Contains(WritingTaskType.Proposal, c1Report.OtherTaskTypes);
        }
    }

    [Fact]
    public async Task Every_profile_criterion_exists_in_its_rubric_or_the_custom_criteria()
    {
        await using var db = Context(Alice);
        await new DataSeeder(NullLogger<DataSeeder>.Instance).SeedAsync(db);
        var rubrics = await db.Rubrics.ToListAsync();
        var customKeys = rubrics.Where(r => r.Kind == RubricKind.Custom).SelectMany(r => r.Criteria).Select(c => c.Key);

        foreach (var profile in await db.Profiles.ToListAsync())
        {
            var available = rubrics.Single(r => r.Id == profile.RubricId).Criteria.Select(c => c.Key).Concat(customKeys).ToHashSet();
            Assert.All(profile.CriterionKeys, key => Assert.Contains(key, available));
        }
    }

    [Theory]
    [InlineData(CefrLevel.A2, WritingTaskType.Story, "a2-story")]
    [InlineData(CefrLevel.A2, WritingTaskType.Letter, "a2-email")]
    [InlineData(CefrLevel.B1, WritingTaskType.Story, "b1-article-story")]
    [InlineData(CefrLevel.B2, WritingTaskType.Essay, "b2-essay")]
    [InlineData(CefrLevel.B2, WritingTaskType.Email, "b2-email")]
    [InlineData(CefrLevel.B2, WritingTaskType.Report, "b2-review")]
    [InlineData(CefrLevel.C1, WritingTaskType.Essay, "c1-essay")]
    [InlineData(CefrLevel.C1, WritingTaskType.Proposal, "c1-report")]
    [InlineData(CefrLevel.C2, WritingTaskType.Essay, "c2-essay")]
    [InlineData(CefrLevel.C2, WritingTaskType.Review, "c2-part2")]
    [InlineData(CefrLevel.C2, WritingTaskType.Other, "c2-essay")]
    [InlineData(CefrLevel.A1, WritingTaskType.Essay, null)]
    public async Task Level_and_task_type_pick_the_matching_exam_profile(CefrLevel level, WritingTaskType taskType, string? expectedKey)
    {
        await using var db = Context(Alice);
        await new DataSeeder(NullLogger<DataSeeder>.Instance).SeedAsync(db);

        var match = ProfileMatcher.FindExamProfile(await db.Profiles.ToListAsync(), await db.Rubrics.ToListAsync(), level, taskType);

        Assert.Equal(expectedKey, match?.Key);
    }

    private sealed class TestUser(Guid id) : ICurrentUser
    {
        public Guid UserId => id;

        public bool IsAuthenticated => true;
    }
}
