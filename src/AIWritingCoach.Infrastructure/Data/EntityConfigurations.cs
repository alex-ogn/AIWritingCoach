using AIWritingCoach.Core.Reviews;
using AIWritingCoach.Core.Rubrics;
using AIWritingCoach.Core.Usage;
using AIWritingCoach.Core.Vocabulary;
using AIWritingCoach.Core.Writing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AIWritingCoach.Infrastructure.Data;

// Portable mappings only: no provider-specific column types or SQL.

internal sealed class WritingDocumentConfiguration : IEntityTypeConfiguration<WritingDocument>
{
    public void Configure(EntityTypeBuilder<WritingDocument> builder)
    {
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Title).HasMaxLength(200);
        builder.Property(e => e.TaskPrompt).HasMaxLength(4000);
        builder.HasIndex(e => new { e.UserId, e.UpdatedAt });
    }
}

internal sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Model).HasMaxLength(200);
        builder.Property(e => e.PromptTemplateKey).HasMaxLength(100);
        builder.Ignore(e => e.ScorePercent);

        builder.HasOne<WritingDocument>().WithMany().HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.Errors).WithOne().HasForeignKey(e => e.ReviewId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.Scores).WithOne().HasForeignKey(e => e.ReviewId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.VocabularySuggestions).WithOne().HasForeignKey(e => e.ReviewId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.UserId, e.CreatedAt });
        builder.HasIndex(e => e.DocumentId);
    }
}

internal sealed class ReviewErrorConfiguration : IEntityTypeConfiguration<ReviewError>
{
    public void Configure(EntityTypeBuilder<ReviewError> builder)
    {
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Original).HasMaxLength(2000);
        builder.Property(e => e.Correction).HasMaxLength(2000);
        builder.Property(e => e.Category).HasMaxLength(64);
        builder.Property(e => e.Subcategory).HasMaxLength(128);
        builder.HasIndex(e => e.Category);
    }
}

internal sealed class CriterionScoreConfiguration : IEntityTypeConfiguration<CriterionScore>
{
    public void Configure(EntityTypeBuilder<CriterionScore> builder)
    {
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.CriterionKey).HasMaxLength(64);
        builder.Property(e => e.CriterionName).HasMaxLength(128);
    }
}

internal sealed class VocabularySuggestionConfiguration : IEntityTypeConfiguration<VocabularySuggestion>
{
    public void Configure(EntityTypeBuilder<VocabularySuggestion> builder)
    {
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Original).HasMaxLength(500);
        builder.Property(e => e.Suggestion).HasMaxLength(500);
        builder.Property(e => e.Example).HasMaxLength(1000);
    }
}

internal sealed class RubricConfiguration : IEntityTypeConfiguration<Rubric>
{
    public void Configure(EntityTypeBuilder<Rubric> builder)
    {
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Key).HasMaxLength(100);
        builder.Property(e => e.Name).HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.Criteria)
            .HasConversion(new JsonTextConverter<List<RubricCriterion>>(), new JsonValueComparer<List<RubricCriterion>>());
        builder.HasIndex(e => new { e.UserId, e.Key }).IsUnique();
    }
}

internal sealed class ProfileConfiguration : IEntityTypeConfiguration<Profile>
{
    public void Configure(EntityTypeBuilder<Profile> builder)
    {
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Key).HasMaxLength(100);
        builder.Property(e => e.Name).HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.Model).HasMaxLength(200);
        builder.PrimitiveCollection(e => e.OtherTaskTypes).ElementType().HasConversion<string>();
        builder.HasOne<Rubric>().WithMany().HasForeignKey(e => e.RubricId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.UserId, e.Key }).IsUnique();
    }
}

internal sealed class PromptTemplateConfiguration : IEntityTypeConfiguration<PromptTemplate>
{
    public void Configure(EntityTypeBuilder<PromptTemplate> builder)
    {
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Key).HasMaxLength(100);
        builder.HasIndex(e => new { e.UserId, e.Key, e.Version }).IsUnique();
    }
}

internal sealed class VocabularyItemConfiguration : IEntityTypeConfiguration<VocabularyItem>
{
    public void Configure(EntityTypeBuilder<VocabularyItem> builder)
    {
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Word).HasMaxLength(200);
        builder.Property(e => e.Replaces).HasMaxLength(200);
        builder.Property(e => e.Example).HasMaxLength(1000);
        builder.HasIndex(e => new { e.UserId, e.Word });
    }
}

internal sealed class AiUsageLogConfiguration : IEntityTypeConfiguration<AiUsageLog>
{
    public void Configure(EntityTypeBuilder<AiUsageLog> builder)
    {
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Feature).HasMaxLength(64);
        builder.Property(e => e.Model).HasMaxLength(200);
        builder.Property(e => e.Error).HasMaxLength(2000);
        builder.HasIndex(e => new { e.UserId, e.CreatedAt });
    }
}
