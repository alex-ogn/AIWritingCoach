using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AIWritingCoach.Infrastructure.Data;

/// <summary>Stores UTC and restores <see cref="DateTimeKind.Utc"/>, which SQL Server and SQLite drop.</summary>
internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

internal sealed class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
    v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v.Value : v.Value.ToUniversalTime()) : v,
    v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

/// <summary>Maps a value to a portable JSON text column (no provider-specific JSON types).</summary>
internal sealed class JsonTextConverter<T>() : ValueConverter<T, string>(
    v => JsonSerializer.Serialize(v, JsonDefaults.Options),
    v => JsonSerializer.Deserialize<T>(v, JsonDefaults.Options)!)
    where T : class;

internal sealed class JsonValueComparer<T>() : ValueComparer<T>(
    (a, b) => JsonSerializer.Serialize(a, JsonDefaults.Options) == JsonSerializer.Serialize(b, JsonDefaults.Options),
    v => JsonSerializer.Serialize(v, JsonDefaults.Options).GetHashCode(StringComparison.Ordinal),
    v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, JsonDefaults.Options), JsonDefaults.Options)!)
    where T : class;

internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
