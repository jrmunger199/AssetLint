using AssetLint.Core;
using AssetLint.Core.Rules;
using static AssetLint.Tests.TestData;

namespace AssetLint.Tests;

public class ValidatorTests
{
    [Fact]
    public async Task Clean_sample_has_no_findings_even_with_disk_checks()
    {
        var ct = TestContext.Current.CancellationToken;
        var manifest = await ManifestLoader.LoadAsync(Sample("hero-pack", "clean.manifest.json"), ct);

        var report = Validator.CreateDefault().Validate(manifest, rootDirectory: Sample("hero-pack", "assets"));

        Assert.Empty(report.Findings);
        Assert.False(report.HasFindingsAtOrAbove(Severity.Info));
    }

    [Fact]
    public async Task Broken_sample_triggers_every_rule_that_needs_no_disk()
    {
        var ct = TestContext.Current.CancellationToken;
        var manifest = await ManifestLoader.LoadAsync(Sample("hero-pack", "broken.manifest.json"), ct);

        var report = Validator.CreateDefault().Validate(manifest);

        var fired = report.Findings.Select(f => f.RuleId).ToHashSet();
        var expected = RuleCatalog.BuiltIn.Select(r => r.Id).Except(["file-exists", "size-mismatch"]);
        Assert.Superset(expected.ToHashSet(), fired);
    }

    [Fact]
    public void Findings_are_ordered_by_severity_then_rule()
    {
        var manifest = Manifest(Asset("Bad Id", path: @"a\b.png"), Asset("Bad Id"));

        var report = Validator.CreateDefault().Validate(manifest);

        Assert.Equal(report.Findings.OrderByDescending(f => f.Severity).ThenBy(f => f.RuleId, StringComparer.Ordinal), report.Findings);
        Assert.Equal(Severity.Error, report.Findings[0].Severity);
    }

    [Fact]
    public void Output_is_deterministic_across_runs()
    {
        var manifest = Manifest(Enumerable.Range(0, 200).Select(i => Asset($"BAD{i}", path: $@"x\{i}.png", type: "nope")).ToArray());
        var validator = Validator.CreateDefault();

        var first = validator.Validate(manifest).Findings;

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(first, validator.Validate(manifest).Findings);
        }
    }

    [Fact]
    public void Config_can_disable_a_rule_and_override_severity()
    {
        var manifest = Manifest(Asset("BadId", type: "nope"));
        var config = new LintConfig
        {
            Rules = new Dictionary<string, RuleSetting>
            {
                ["unknown-type"] = new() { Enabled = false },
                ["naming-convention"] = new() { Severity = Severity.Error },
            },
        };

        var report = Validator.CreateDefault().Validate(manifest, config);

        var finding = Assert.Single(report.Findings);
        Assert.Equal("naming-convention", finding.RuleId);
        Assert.Equal(Severity.Error, finding.Severity);
    }

    [Fact]
    public void Severity_only_override_does_not_disable_the_rule()
    {
        // Regression: a JSON setting with just "severity" used to deserialize with Enabled = false.
        var config = LintConfig.Parse("""{ "rules": { "naming-convention": { "severity": "error" } } }""");

        var report = Validator.CreateDefault().Validate(Manifest(Asset("BadId")), config);

        Assert.Contains(report.Findings, f => f is { RuleId: "naming-convention", Severity: Severity.Error });
    }

    [Fact]
    public void Partial_config_keeps_defaults_for_missing_keys()
    {
        var config = LintConfig.Parse("""{ "namingPattern": "^x$" }""");

        Assert.Equal("^x$", config.NamingPattern);
        Assert.Contains("texture", config.KnownTypes);
        Assert.True(config.MaxSizeBytes.ContainsKey("texture"));
        Assert.Empty(config.Rules);
    }

    [Fact]
    public void Invalid_config_raises_a_friendly_error() =>
        Assert.Throws<AssetLintException>(() => LintConfig.Parse("{ nope"));

    [Fact]
    public void A_crashing_rule_becomes_a_finding_instead_of_failing_the_run()
    {
        var validator = new Validator([new ThrowingRule(), new DuplicateIdRule()]);

        var report = validator.Validate(Manifest(Asset("a.one"), Asset("a.one")));

        Assert.Contains(report.Findings, f => f.RuleId == "boom" && f.Message.Contains("kaput", StringComparison.Ordinal));
        Assert.Contains(report.Findings, f => f.RuleId == "duplicate-id");
    }

    [Fact]
    public void Custom_rules_plug_in_through_the_interface()
    {
        var validator = new Validator([new NoDataTypeRule()]);

        var report = validator.Validate(Manifest(Asset("a.one", type: "data")));

        Assert.Equal("no-data", Assert.Single(report.Findings).RuleId);
    }

    private sealed class ThrowingRule : IRule
    {
        public string Id => "boom";

        public string Description => "Always fails.";

        public Severity DefaultSeverity => Severity.Error;

        public IEnumerable<Finding> Evaluate(RuleContext context) => throw new InvalidOperationException("kaput");
    }

    private sealed class NoDataTypeRule() : RuleBase("no-data", "Disallows data assets.", Severity.Warning)
    {
        public override IEnumerable<Finding> Evaluate(RuleContext context) =>
            context.Manifest.Assets.Where(a => a.Type == "data").Select(a => Report(a, "Data assets are not allowed."));
    }
}
