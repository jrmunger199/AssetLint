using AssetLint.Core.Rules;

namespace AssetLint.Core;

/// <summary>The outcome of validating one manifest.</summary>
public sealed record ValidationReport(
    string Manifest,
    string? Source,
    int AssetCount,
    IReadOnlyList<Finding> Findings)
{
    public int ErrorCount => Count(Severity.Error);

    public int WarningCount => Count(Severity.Warning);

    public int InfoCount => Count(Severity.Info);

    /// <summary>True when any finding is at least as severe as <paramref name="threshold"/>.</summary>
    public bool HasFindingsAtOrAbove(Severity threshold) => Findings.Any(f => f.Severity >= threshold);

    private int Count(Severity severity) => Findings.Count(f => f.Severity == severity);
}

/// <summary>Runs a set of rules over a manifest and applies config overrides to the results.</summary>
public sealed class Validator
{
    private readonly IReadOnlyList<IRule> _rules;

    public Validator(IEnumerable<IRule> rules) => _rules = [.. rules];

    public static Validator CreateDefault() => new(RuleCatalog.BuiltIn);

    public ValidationReport Validate(AssetManifest manifest, LintConfig? config = null, string? rootDirectory = null)
    {
        config ??= LintConfig.Default;
        var context = new RuleContext(manifest, config, rootDirectory);

        // Rules are independent and read-only, so they run in parallel; AsOrdered keeps output deterministic.
        var findings = _rules
            .Where(rule => !config.Rules.TryGetValue(rule.Id, out var setting) || setting.IsEnabled)
            .AsParallel()
            .AsOrdered()
            .SelectMany(rule => Run(rule, context))
            .ToList();

        var ordered = findings
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.RuleId, StringComparer.Ordinal)
            .ToList();

        return new ValidationReport(manifest.Name, manifest.SourcePath, manifest.Assets.Count, ordered);
    }

    private static List<Finding> Run(IRule rule, RuleContext context)
    {
        try
        {
            var severity = context.Config.Rules.TryGetValue(rule.Id, out var setting) ? setting.Severity : null;
            return
            [
                .. rule.Evaluate(context)
                    .Select(f => severity is { } overridden ? f with { Severity = overridden } : f),
            ];
        }
#pragma warning disable CA1031 // A faulty rule must not take down the whole run; the failure is reported as a finding.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return [new Finding(rule.Id, Severity.Error, $"Rule failed to run: {ex.Message}")];
        }
    }
}
