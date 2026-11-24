using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssetLint.Core;

/// <summary>Per-rule overrides from the config file.</summary>
public sealed record RuleSetting
{
    /// <summary>Set to <c>false</c> to switch the rule off. Absent means enabled.</summary>
    public bool? Enabled { get; init; }

    /// <summary>When set, replaces the rule's default severity.</summary>
    public Severity? Severity { get; init; }

    [JsonIgnore]
    public bool IsEnabled => Enabled ?? true;
}

/// <summary>Tunable settings for a lint run, normally loaded from <c>assetlint.json</c>.</summary>
public sealed record LintConfig
{
    public const string FileName = "assetlint.json";

    public static LintConfig Default { get; } = new();

    // Every setting falls back to its default through `field` (C# 14), so a config file that
    // mentions only some keys still yields a complete configuration.

    public IReadOnlyDictionary<string, RuleSetting> Rules
    {
        get => field ?? new Dictionary<string, RuleSetting>();
        init;
    }

    /// <summary>Regex every asset id must match (rule <c>naming-convention</c>).</summary>
    public string NamingPattern { get => field ?? @"^[a-z0-9]+(\.[a-z0-9_]+)+$"; init; }

    /// <summary>Asset types the project recognises (rule <c>unknown-type</c>).</summary>
    public IReadOnlyList<string> KnownTypes
    {
        get => field ?? ["texture", "mesh", "audio", "shader", "material", "animation", "script", "data"];
        init;
    }

    /// <summary>Largest allowed file size per asset type (rule <c>max-size</c>).</summary>
    public IReadOnlyDictionary<string, long> MaxSizeBytes
    {
        get => field ?? new Dictionary<string, long>
        {
            ["texture"] = 16 * 1024 * 1024,
            ["mesh"] = 32 * 1024 * 1024,
            ["audio"] = 64 * 1024 * 1024,
        };
        init;
    }

    public static LintConfig Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, AssetLintJsonContext.Default.LintConfig)
                ?? throw new AssetLintException("Config file is empty.");
        }
        catch (JsonException ex)
        {
            throw new AssetLintException($"Invalid config: {ex.Message}", ex);
        }
    }

    public static async Task<LintConfig> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Parse(await File.ReadAllTextAsync(path, cancellationToken));
}
