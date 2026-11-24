using System.Text.Json.Serialization;

namespace AssetLint.Core;

/// <summary>How serious a finding is. Ordered so that comparisons work (Error &gt; Warning &gt; Info).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Severity>))]
public enum Severity
{
    Info = 0,
    Warning = 1,
    Error = 2,
}

/// <summary>A single asset declared in a manifest.</summary>
public sealed record AssetEntry
{
    public required string Id { get; init; }
    public required string Path { get; init; }
    public required string Type { get; init; }
    public long SizeBytes { get; init; }

    // The `field` keyword (C# 14) keeps these null-safe even when a deserializer leaves an
    // absent JSON property at default(null) instead of the initializer value.
    public IReadOnlyList<string> Tags { get => field ?? []; init; }

    public IReadOnlyList<string> Dependencies { get => field ?? []; init; }

    /// <summary>Zero-based position in the manifest; used for locations in reports.</summary>
    [JsonIgnore]
    public int Index { get; init; }

    [JsonIgnore]
    public string Location => $"assets[{Index}]";
}

/// <summary>A parsed asset manifest.</summary>
public sealed record AssetManifest
{
    public required string Name { get; init; }
    public string Version { get => field ?? "0.0.0"; init; }

    public IReadOnlyList<AssetEntry> Assets { get => field ?? []; init; }

    /// <summary>File the manifest was loaded from, when known.</summary>
    [JsonIgnore]
    public string? SourcePath { get; init; }
}

/// <summary>One problem reported by a rule.</summary>
public sealed record Finding(
    string RuleId,
    Severity Severity,
    string Message,
    string? AssetId = null,
    string? Location = null);
