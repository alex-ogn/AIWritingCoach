using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIWritingCoach.Core.Rubrics;
using AIWritingCoach.Core.Writing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AIWritingCoach.Infrastructure.Data;

/// <summary>
/// Upserts the system rubrics, prompt templates and profiles embedded under <c>Seed/</c>.
/// A system row is only replaced when the seed file has a higher <c>version</c>; user-owned rows are never touched.
/// </summary>
public sealed class DataSeeder(ILogger<DataSeeder> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        foreach (var seed in Load<RubricSeed>("Rubrics"))
        {
            var existing = await db.Rubrics.IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.UserId == null && r.Key == seed.Key, cancellationToken);
            if (existing is null)
            {
                db.Rubrics.Add(seed.Apply(new Rubric()));
            }
            else if (seed.Version > existing.Version)
            {
                seed.Apply(existing);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (var seed in Load<PromptSeed>("Prompts"))
        {
            var exists = await db.PromptTemplates.IgnoreQueryFilters()
                .AnyAsync(t => t.UserId == null && t.Key == seed.Key && t.Version == seed.Version, cancellationToken);
            if (!exists)
            {
                db.PromptTemplates.Add(new PromptTemplate
                {
                    Key = seed.Key,
                    Version = seed.Version,
                    SystemPrompt = string.Join('\n', seed.SystemPrompt),
                    UserTemplate = string.Join('\n', seed.UserTemplate),
                });
            }
        }

        var rubricIds = await db.Rubrics.IgnoreQueryFilters()
            .Where(r => r.UserId == null)
            .ToDictionaryAsync(r => r.Key, r => r.Id, cancellationToken);

        foreach (var seed in Load<ProfileSeed>("Profiles"))
        {
            if (!rubricIds.TryGetValue(seed.RubricKey, out var rubricId))
            {
                logger.LogWarning("Profile seed {Profile} references unknown rubric {Rubric}; skipped", seed.Key, seed.RubricKey);
                continue;
            }

            var existing = await db.Profiles.IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.UserId == null && p.Key == seed.Key, cancellationToken);
            if (existing is null)
            {
                db.Profiles.Add(seed.Apply(new Profile(), rubricId));
            }
            else if (seed.Version > existing.Version)
            {
                seed.Apply(existing, rubricId);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    internal static IEnumerable<T> Load<T>(string folder)
    {
        var assembly = typeof(DataSeeder).Assembly;
        var prefix = $"{assembly.GetName().Name}.Seed.{folder}.";
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(prefix, StringComparison.Ordinal)).Order())
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            yield return JsonSerializer.Deserialize<T>(stream, JsonOptions)
                ?? throw new InvalidDataException($"Seed file {name} is empty.");
        }
    }

    internal sealed record RubricSeed(string Key, int Version, string Name, string? Description, RubricKind Kind, List<RubricCriterion> Criteria)
    {
        public Rubric Apply(Rubric rubric)
        {
            rubric.Key = Key;
            rubric.Version = Version;
            rubric.Name = Name;
            rubric.Description = Description;
            rubric.Kind = Kind;
            rubric.Criteria = Criteria;
            return rubric;
        }
    }

    internal sealed record PromptSeed(string Key, int Version, string[] SystemPrompt, string[] UserTemplate);

    internal sealed record ProfileSeed(
        string Key,
        int Version,
        string Name,
        string? Description,
        CefrLevel Level,
        WritingTaskType TaskType,
        string RubricKey,
        List<string> CriterionKeys,
        string? Model,
        int? MinWords,
        int? MaxWords,
        int SortOrder)
    {
        public Profile Apply(Profile profile, Guid rubricId)
        {
            profile.Key = Key;
            profile.Version = Version;
            profile.Name = Name;
            profile.Description = Description;
            profile.TargetLevel = Level;
            profile.TaskType = TaskType;
            profile.RubricId = rubricId;
            profile.CriterionKeys = CriterionKeys;
            profile.Model = Model;
            profile.MinWords = MinWords;
            profile.MaxWords = MaxWords;
            profile.SortOrder = SortOrder;
            return profile;
        }
    }
}
