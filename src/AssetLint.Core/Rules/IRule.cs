namespace AssetLint.Core.Rules;

/// <summary>Everything a rule may look at. Rules must treat it as read-only.</summary>
public sealed record RuleContext(AssetManifest Manifest, LintConfig Config, string? RootDirectory = null);

/// <summary>A single check. Implement this to add your own rule.</summary>
public interface IRule
{
    /// <summary>Stable kebab-case identifier used in reports and config.</summary>
    string Id { get; }

    string Description { get; }

    Severity DefaultSeverity { get; }

    IEnumerable<Finding> Evaluate(RuleContext context);
}

/// <summary>Convenience base that fills in the metadata and builds findings.</summary>
public abstract class RuleBase(string id, string description, Severity defaultSeverity) : IRule
{
    public string Id { get; } = id;

    public string Description { get; } = description;

    public Severity DefaultSeverity { get; } = defaultSeverity;

    public abstract IEnumerable<Finding> Evaluate(RuleContext context);

    protected Finding Report(AssetEntry? asset, string message) =>
        new(Id, DefaultSeverity, message, asset?.Id, asset?.Location);
}
