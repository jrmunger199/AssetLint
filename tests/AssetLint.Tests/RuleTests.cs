using AssetLint.Core;
using AssetLint.Core.Rules;
using static AssetLint.Tests.TestData;

namespace AssetLint.Tests;

public class RuleTests
{
    private static List<Finding> Run(IRule rule, AssetManifest manifest, LintConfig? config = null, string? root = null) =>
        [.. rule.Evaluate(new RuleContext(manifest, config ?? LintConfig.Default, root))];

    [Fact]
    public void DuplicateId_flags_the_second_occurrence_ignoring_case()
    {
        var findings = Run(new DuplicateIdRule(), Manifest(Asset("tex.a"), Asset("TEX.A"), Asset("tex.b")));

        var finding = Assert.Single(findings);
        Assert.Equal("assets[1]", finding.Location);
        Assert.Contains("assets[0]", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicatePath_ignores_slash_style_and_case()
    {
        var findings = Run(new DuplicatePathRule(),
            Manifest(Asset("a.one", path: "x/y.png"), Asset("a.two", path: @"X\Y.png")));

        Assert.Single(findings);
    }

    [Fact]
    public void MissingDependency_names_the_absent_asset()
    {
        var findings = Run(new MissingDependencyRule(),
            Manifest(Asset("a.one", dependencies: ["a.two", "ghost.asset"]), Asset("a.two")));

        var finding = Assert.Single(findings);
        Assert.Contains("ghost.asset", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CircularDependency_reports_the_full_cycle()
    {
        var manifest = Manifest(
            Asset("a.one", dependencies: "a.two"),
            Asset("a.two", dependencies: "a.three"),
            Asset("a.three", dependencies: "a.one"));

        var finding = Assert.Single(Run(new CircularDependencyRule(), manifest));

        Assert.Contains("a.one -> a.two -> a.three -> a.one", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CircularDependency_catches_self_reference()
    {
        var finding = Assert.Single(Run(new CircularDependencyRule(), Manifest(Asset("a.one", dependencies: "a.one"))));

        Assert.Contains("a.one -> a.one", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CircularDependency_accepts_a_diamond()
    {
        // a -> b, a -> c, b -> d, c -> d is shared but not circular.
        var manifest = Manifest(
            Asset("a.top", dependencies: ["a.left", "a.right"]),
            Asset("a.left", dependencies: "a.base"),
            Asset("a.right", dependencies: "a.base"),
            Asset("a.base"));

        Assert.Empty(Run(new CircularDependencyRule(), manifest));
    }

    [Fact]
    public void CircularDependency_handles_a_long_chain()
    {
        var assets = Enumerable.Range(0, 2000)
            .Select(i => Asset($"chain.n{i}", dependencies: i < 1999 ? [$"chain.n{i + 1}"] : []))
            .ToArray();

        Assert.Empty(Run(new CircularDependencyRule(), Manifest(assets)));
    }

    [Theory]
    [InlineData("tex.hero.diffuse", true)]
    [InlineData("Tex.Hero", false)]
    [InlineData("nodots", false)]
    [InlineData("has space.png", false)]
    public void NamingConvention_uses_the_default_pattern(string id, bool valid) =>
        Assert.Equal(valid, Run(new NamingConventionRule(), Manifest(Asset(id))).Count == 0);

    [Fact]
    public void NamingConvention_honours_a_custom_pattern()
    {
        var config = new LintConfig { NamingPattern = "^[A-Z]+$" };

        Assert.Empty(Run(new NamingConventionRule(), Manifest(Asset("HERO")), config));
        Assert.Single(Run(new NamingConventionRule(), Manifest(Asset("hero")), config));
    }

    [Fact]
    public void UnknownType_is_case_insensitive()
    {
        var findings = Run(new UnknownTypeRule(), Manifest(Asset("a.one", type: "Texture"), Asset("a.two", type: "fx")));

        Assert.Equal("assets[1]", Assert.Single(findings).Location);
    }

    [Fact]
    public void MaxSize_flags_oversized_and_negative_sizes()
    {
        var config = new LintConfig { MaxSizeBytes = new Dictionary<string, long> { ["texture"] = 1000 } };
        var findings = Run(new MaxSizeRule(),
            Manifest(Asset("a.ok", size: 1000), Asset("a.big", size: 1001), Asset("a.neg", size: -5), Asset("a.mesh", type: "mesh", size: 1 << 30)),
            config);

        Assert.Equal(["assets[1]", "assets[2]"], findings.Select(f => f.Location));
        Assert.Equal(Severity.Error, findings[1].Severity);
    }

    [Theory]
    [InlineData(@"a\b.png", 1)]
    [InlineData(" a/b.png", 1)]
    [InlineData(@" a\b.png ", 2)]
    [InlineData("a/b.png", 0)]
    public void PathStyle_counts_each_problem(string path, int expected) =>
        Assert.Equal(expected, Run(new PathStyleRule(), Manifest(Asset("a.one", path: path))).Count);

    [Theory]
    [InlineData("../secret.txt", false)]
    [InlineData("a/../../secret.txt", false)]
    [InlineData("/etc/passwd", false)]
    [InlineData(@"C:\Windows\system.ini", false)]
    [InlineData(@"..\secret.txt", false)]
    [InlineData("a/b/c.png", true)]
    [InlineData("a/b..c/d.png", true)]
    public void PathTraversal_rejects_paths_that_escape_the_root(string path, bool safe) =>
        Assert.Equal(safe, Run(new PathTraversalRule(), Manifest(Asset("a.one", path: path))).Count == 0);

    [Fact]
    public void FileSystem_rules_do_nothing_without_a_root()
    {
        var manifest = Manifest(Asset("a.one"));

        Assert.Empty(Run(new FileExistsRule(), manifest));
        Assert.Empty(Run(new SizeMismatchRule(), manifest));
    }

    [Fact]
    public void FileExists_and_SizeMismatch_check_the_disk()
    {
        using var temp = new TempDirectory();
        File.WriteAllBytes(Path.Combine(temp.Path, "present.bin"), new byte[100]);
        var manifest = Manifest(
            Asset("a.good", path: "present.bin", size: 100),
            Asset("a.wrongsize", path: "present.bin", size: 7),
            Asset("a.missing", path: "absent.bin"),
            Asset("a.escape", path: "../present.bin"));

        var missing = Run(new FileExistsRule(), manifest, root: temp.Path);
        var mismatch = Run(new SizeMismatchRule(), manifest, root: temp.Path);

        Assert.Equal("assets[2]", Assert.Single(missing).Location);
        Assert.Equal("assets[1]", Assert.Single(mismatch).Location);
    }

    [Fact]
    public void Built_in_rule_ids_are_unique_and_kebab_case()
    {
        var ids = RuleCatalog.BuiltIn.Select(r => r.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ids, id => Assert.Matches("^[a-z]+(-[a-z]+)*$", id));
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateDirectory(
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "assetlint-" + Guid.NewGuid().ToString("N"))).FullName;

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
