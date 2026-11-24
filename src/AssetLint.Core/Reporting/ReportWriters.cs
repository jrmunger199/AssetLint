using System.Text;
using System.Text.Json;
using AssetLint.Core.Rules;

namespace AssetLint.Core.Reporting;

public enum ReportFormat
{
    Text,
    Json,
    Sarif,
}

public interface IReportWriter
{
    void Write(ValidationReport report, TextWriter output);
}

public static class ReportWriter
{
    public static IReportWriter Create(ReportFormat format, bool useColor = false) => format switch
    {
        ReportFormat.Text => new TextReportWriter(useColor),
        ReportFormat.Json => new JsonReportWriter(),
        ReportFormat.Sarif => new SarifReportWriter(),
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };
}

/// <summary>Human-friendly, column-aligned output for terminals.</summary>
public sealed class TextReportWriter(bool useColor) : IReportWriter
{
    public void Write(ValidationReport report, TextWriter output)
    {
        output.WriteLine($"{report.Manifest} ({report.AssetCount} assets)");
        output.WriteLine();

        if (report.Findings.Count == 0)
        {
            output.WriteLine("No issues found.");
            return;
        }

        var ruleWidth = report.Findings.Max(f => f.RuleId.Length);
        foreach (var finding in report.Findings)
        {
            var label = Paint(finding.Severity.ToString().ToLowerInvariant().PadRight(7), finding.Severity);
            var location = finding.Location is null ? string.Empty : $"{finding.Location} ";
            output.WriteLine($"  {label} {finding.RuleId.PadRight(ruleWidth)}  {location}{finding.Message}");
        }

        output.WriteLine();
        output.WriteLine(
            $"{Plural(report.ErrorCount, "error")}, {Plural(report.WarningCount, "warning")}, {report.InfoCount} info");
    }

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

    private string Paint(string text, Severity severity)
    {
        if (!useColor)
        {
            return text;
        }

        var code = severity switch
        {
            Severity.Error => "31",
            Severity.Warning => "33",
            _ => "36",
        };
        return $"\u001b[{code}m{text}\u001b[0m";
    }
}

/// <summary>Machine-readable output for scripts and dashboards.</summary>
public sealed class JsonReportWriter : IReportWriter
{
    public void Write(ValidationReport report, TextWriter output) =>
        output.WriteLine(JsonSerializer.Serialize(report, AssetLintJsonContext.Default.ValidationReport));
}

/// <summary>SARIF 2.1.0, the format GitHub code scanning and many IDEs consume.</summary>
public sealed class SarifReportWriter : IReportWriter
{
    public void Write(ValidationReport report, TextWriter output)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartObject();
            json.WriteString("$schema", "https://json.schemastore.org/sarif-2.1.0.json");
            json.WriteString("version", "2.1.0");
            json.WriteStartArray("runs");
            json.WriteStartObject();

            WriteTool(json);
            WriteResults(json, report);

            json.WriteEndObject();
            json.WriteEndArray();
            json.WriteEndObject();
        }

        output.WriteLine(Encoding.UTF8.GetString(stream.ToArray()));
    }

    private static void WriteTool(Utf8JsonWriter json)
    {
        json.WriteStartObject("tool");
        json.WriteStartObject("driver");
        json.WriteString("name", "AssetLint");
        json.WriteString("version", typeof(SarifReportWriter).Assembly.GetName().Version?.ToString(3) ?? "1.0.0");
        json.WriteStartArray("rules");
        foreach (var rule in RuleCatalog.BuiltIn)
        {
            json.WriteStartObject();
            json.WriteString("id", rule.Id);
            json.WriteStartObject("shortDescription");
            json.WriteString("text", rule.Description);
            json.WriteEndObject();
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteEndObject();
        json.WriteEndObject();
    }

    private static void WriteResults(Utf8JsonWriter json, ValidationReport report)
    {
        json.WriteStartArray("results");
        foreach (var finding in report.Findings)
        {
            json.WriteStartObject();
            json.WriteString("ruleId", finding.RuleId);
            json.WriteString("level", finding.Severity switch
            {
                Severity.Error => "error",
                Severity.Warning => "warning",
                _ => "note",
            });
            json.WriteStartObject("message");
            json.WriteString("text", finding.Message);
            json.WriteEndObject();

            json.WriteStartArray("locations");
            json.WriteStartObject();
            json.WriteStartObject("physicalLocation");
            json.WriteStartObject("artifactLocation");
            json.WriteString("uri", (report.Source ?? report.Manifest).Replace('\\', '/'));
            json.WriteEndObject();
            json.WriteEndObject();
            if (finding.AssetId is not null)
            {
                json.WriteStartArray("logicalLocations");
                json.WriteStartObject();
                json.WriteString("name", finding.AssetId);
                json.WriteString("fullyQualifiedName", finding.Location ?? finding.AssetId);
                json.WriteEndObject();
                json.WriteEndArray();
            }

            json.WriteEndObject();
            json.WriteEndArray();
            json.WriteEndObject();
        }

        json.WriteEndArray();
    }
}
