using System.Text.Json;
using AssetLint.Core;
using AssetLint.Core.Reporting;

namespace AssetLint.Tests;

public class ReportWriterTests
{
    private static readonly ValidationReport Sample = new(
        "Pack",
        @"C:\data\pack.json",
        3,
        [
            new Finding("duplicate-id", Severity.Error, "dup", "a.one", "assets[1]"),
            new Finding("naming-convention", Severity.Warning, "naming", "BadId", "assets[2]"),
        ]);

    private static string Render(ReportFormat format, ValidationReport? report = null, bool color = false)
    {
        using var writer = new StringWriter();
        ReportWriter.Create(format, color).Write(report ?? Sample, writer);
        return writer.ToString();
    }

    [Fact]
    public void Text_lists_findings_and_a_summary()
    {
        var text = Render(ReportFormat.Text);

        Assert.Contains("Pack (3 assets)", text, StringComparison.Ordinal);
        Assert.Contains("error   duplicate-id", text, StringComparison.Ordinal);
        Assert.Contains("1 error, 1 warning, 0 info", text, StringComparison.Ordinal);
        Assert.DoesNotContain('\u001b', text);
    }

    [Fact]
    public void Text_uses_ansi_colour_only_when_asked()
    {
        Assert.Contains("\u001b[31m", Render(ReportFormat.Text, color: true), StringComparison.Ordinal);
    }

    [Fact]
    public void Text_reports_a_clean_manifest()
    {
        var text = Render(ReportFormat.Text, new ValidationReport("Pack", null, 2, []));

        Assert.Contains("No issues found.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_is_valid_and_includes_counts()
    {
        using var document = JsonDocument.Parse(Render(ReportFormat.Json));
        var root = document.RootElement;

        Assert.Equal("Pack", root.GetProperty("manifest").GetString());
        Assert.Equal(1, root.GetProperty("errorCount").GetInt32());
        Assert.Equal("Error", root.GetProperty("findings")[0].GetProperty("severity").GetString());
    }

    [Fact]
    public void Sarif_has_the_required_structure()
    {
        using var document = JsonDocument.Parse(Render(ReportFormat.Sarif));
        var run = document.RootElement.GetProperty("runs")[0];
        var results = run.GetProperty("results");

        Assert.Equal("2.1.0", document.RootElement.GetProperty("version").GetString());
        Assert.Equal("AssetLint", run.GetProperty("tool").GetProperty("driver").GetProperty("name").GetString());
        Assert.Equal(2, results.GetArrayLength());
        Assert.Equal("error", results[0].GetProperty("level").GetString());
        Assert.Equal("warning", results[1].GetProperty("level").GetString());
        Assert.Equal(
            "C:/data/pack.json",
            results[0].GetProperty("locations")[0].GetProperty("physicalLocation").GetProperty("artifactLocation").GetProperty("uri").GetString());
    }
}
