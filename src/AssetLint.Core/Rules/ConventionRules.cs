using System.Text.RegularExpressions;

namespace AssetLint.Core.Rules;

/// <summary>Ids must match the configured pattern.</summary>
public sealed class NamingConventionRule()
    : RuleBase("naming-convention", "Asset ids must match the configured naming pattern.", Severity.Warning)
{
    public override IEnumerable<Finding> Evaluate(RuleContext context)
    {
        // A timeout guards against catastrophic backtracking in a user-supplied pattern.
        var pattern = new Regex(
            context.Config.NamingPattern,
            RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(250));

        foreach (var asset in context.Manifest.Assets.Where(a => !pattern.IsMatch(a.Id)))
        {
            yield return Report(asset, $"Id '{asset.Id}' does not match the pattern '{context.Config.NamingPattern}'.");
        }
    }
}

/// <summary>The asset type must be one the project knows about.</summary>
public sealed class UnknownTypeRule()
    : RuleBase("unknown-type", "Asset type must be one of the configured known types.", Severity.Warning)
{
    public override IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var known = context.Config.KnownTypes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in context.Manifest.Assets.Where(a => !known.Contains(a.Type)))
        {
            yield return Report(asset, $"Type '{asset.Type}' is not recognised. Known types: {string.Join(", ", context.Config.KnownTypes)}.");
        }
    }
}

/// <summary>Declared sizes must be sane and under the per-type budget.</summary>
public sealed class MaxSizeRule()
    : RuleBase("max-size", "Assets must not exceed the size budget for their type.", Severity.Warning)
{
    public override IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var limits = new Dictionary<string, long>(context.Config.MaxSizeBytes, StringComparer.OrdinalIgnoreCase);
        foreach (var asset in context.Manifest.Assets)
        {
            if (asset.SizeBytes < 0)
            {
                yield return new Finding(Id, Severity.Error, $"Declared size {asset.SizeBytes} is negative.", asset.Id, asset.Location);
            }
            else if (limits.TryGetValue(asset.Type, out var limit) && asset.SizeBytes > limit)
            {
                yield return Report(asset, $"Size {Format(asset.SizeBytes)} exceeds the {Format(limit)} budget for '{asset.Type}' assets.");
            }
        }
    }

    private static string Format(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):0.#} MiB",
        >= 1024 => $"{bytes / 1024.0:0.#} KiB",
        _ => $"{bytes} B",
    };
}

/// <summary>Style nits in paths: backslashes and stray whitespace break cross-platform pipelines.</summary>
public sealed class PathStyleRule()
    : RuleBase("path-style", "Paths should use forward slashes and contain no stray whitespace.", Severity.Warning)
{
    public override IEnumerable<Finding> Evaluate(RuleContext context)
    {
        foreach (var asset in context.Manifest.Assets)
        {
            if (asset.Path.Contains('\\'))
            {
                yield return Report(asset, $"Path '{asset.Path}' uses backslashes; use forward slashes.");
            }

            if (asset.Path != asset.Path.Trim())
            {
                yield return Report(asset, $"Path '{asset.Path}' has leading or trailing whitespace.");
            }
        }
    }
}
