using AssetLint.Cli;
using static AssetLint.Tests.TestData;

namespace AssetLint.Tests;

public class CliTests
{
    private static async Task<(int ExitCode, string Output, string Error)> Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = await CliApp.RunAsync(args, output, error, TestContext.Current.CancellationToken);
        return (exit, output.ToString(), error.ToString());
    }

    [Fact]
    public async Task Clean_manifest_exits_zero()
    {
        var (exit, output, _) = await Run("validate", Sample("hero-pack", "clean.manifest.json"), "--root", Sample("hero-pack", "assets"));

        Assert.Equal(0, exit);
        Assert.Contains("No issues found.", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Errors_exit_one()
    {
        var (exit, output, _) = await Run("validate", Sample("hero-pack", "broken.manifest.json"));

        Assert.Equal(1, exit);
        Assert.Contains("circular-dependency", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fail_on_none_always_exits_zero()
    {
        var (exit, _, _) = await Run("validate", Sample("hero-pack", "broken.manifest.json"), "--fail-on", "none");

        Assert.Equal(0, exit);
    }

    [Fact]
    public async Task Fail_on_warning_fails_a_warnings_only_run()
    {
        var manifest = Sample("custom-config", "manifest.json");
        var config = Sample("custom-config", "assetlint.json");
        var relaxed = Path.Combine(Path.GetTempPath(), $"assetlint-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(relaxed, """{ "rules": { "naming-convention": { "severity": "warning" } } }""", TestContext.Current.CancellationToken);

        try
        {
            Assert.Equal(1, (await Run("validate", manifest, "-c", config)).ExitCode); // naming error
            Assert.Equal(0, (await Run("validate", manifest, "-c", relaxed)).ExitCode); // warnings only
            Assert.Equal(1, (await Run("validate", manifest, "-c", relaxed, "--fail-on", "warning")).ExitCode);
        }
        finally
        {
            File.Delete(relaxed);
        }
    }

    [Fact]
    public async Task Config_next_to_the_manifest_is_discovered()
    {
        var (_, output, _) = await Run("validate", Sample("custom-config", "manifest.json"));

        Assert.Contains("naming-convention", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_file_exits_two_with_a_message_on_stderr()
    {
        var (exit, output, error) = await Run("validate", Sample("does-not-exist.json"));

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.StartsWith("error:", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Json_format_is_parseable()
    {
        var (_, output, _) = await Run("validate", Sample("hero-pack", "broken.manifest.json"), "--format", "json");

        using var document = System.Text.Json.JsonDocument.Parse(output);
        Assert.True(document.RootElement.GetProperty("errorCount").GetInt32() > 0);
    }

    [Fact]
    public async Task Output_option_writes_the_report_to_a_file()
    {
        var file = Path.Combine(Path.GetTempPath(), $"assetlint-{Guid.NewGuid():N}.sarif");
        try
        {
            var (exit, output, _) = await Run("validate", Sample("hero-pack", "broken.manifest.json"), "-f", "sarif", "-o", file);

            Assert.Equal(1, exit);
            Assert.Empty(output);
            Assert.Contains("\"version\": \"2.1.0\"", await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task Rules_command_lists_every_built_in_rule()
    {
        var (exit, output, _) = await Run("rules");

        Assert.Equal(0, exit);
        Assert.All(AssetLint.Core.Rules.RuleCatalog.BuiltIn, r => Assert.Contains(r.Id, output, StringComparison.Ordinal));
    }
}
