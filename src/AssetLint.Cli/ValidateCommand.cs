using System.CommandLine;
using AssetLint.Core;
using AssetLint.Core.Reporting;

namespace AssetLint.Cli;

/// <summary>Which finding severity makes the process exit with a failure code.</summary>
public enum FailOn
{
    None,
    Info,
    Warning,
    Error,
}

internal static class ValidateCommand
{
    public const int ExitOk = 0;
    public const int ExitFindings = 1;
    public const int ExitUsage = 2;

    public static Command Create()
    {
        var manifest = new Argument<FileInfo>("manifest")
        {
            Description = "Path to the manifest file (.json or .xml).",
        };
        var root = new Option<DirectoryInfo?>("--root")
        {
            Description = "Asset root directory. Enables the on-disk checks (file-exists, size-mismatch).",
        };
        var config = new Option<FileInfo?>("--config", "-c")
        {
            Description = $"Config file. Defaults to {LintConfig.FileName} next to the manifest, if present.",
        };
        var format = new Option<ReportFormat>("--format", "-f")
        {
            Description = "Report format: text, json or sarif.",
            DefaultValueFactory = _ => ReportFormat.Text,
        };
        var failOn = new Option<FailOn>("--fail-on")
        {
            Description = "Lowest severity that produces a non-zero exit code: none, info, warning or error.",
            DefaultValueFactory = _ => FailOn.Error,
        };
        var outputFile = new Option<FileInfo?>("--output", "-o")
        {
            Description = "Write the report to a file instead of standard output.",
        };
        var noColor = new Option<bool>("--no-color")
        {
            Description = "Disable ANSI colours in text output (also honours the NO_COLOR variable).",
        };

        var command = new Command("validate", "Validate an asset manifest against the rule set.")
        {
            manifest, root, config, format, failOn, outputFile, noColor,
        };

        command.SetAction((parseResult, cancellationToken) =>
        {
            var options = new Options(
                parseResult.GetRequiredValue(manifest),
                parseResult.GetValue(root),
                parseResult.GetValue(config),
                parseResult.GetValue(format),
                parseResult.GetValue(failOn),
                parseResult.GetValue(outputFile),
                parseResult.GetValue(noColor));

            return RunAsync(
                options,
                parseResult.InvocationConfiguration.Output,
                parseResult.InvocationConfiguration.Error,
                cancellationToken);
        });

        return command;
    }

    private sealed record Options(
        FileInfo Manifest,
        DirectoryInfo? Root,
        FileInfo? Config,
        ReportFormat Format,
        FailOn FailOn,
        FileInfo? Output,
        bool NoColor);

    private static async Task<int> RunAsync(
        Options options,
        TextWriter stdout,
        TextWriter stderr,
        CancellationToken cancellationToken)
    {
        try
        {
            var manifest = await ManifestLoader.LoadAsync(options.Manifest.FullName, cancellationToken);
            var config = await LoadConfigAsync(options, cancellationToken);
            var report = Validator.CreateDefault().Validate(manifest, config, options.Root?.FullName);

            if (options.Output is null)
            {
                var color = options.Format == ReportFormat.Text && ColorAllowed(options);
                ReportWriter.Create(options.Format, color).Write(report, stdout);
            }
            else
            {
                await using var file = new StreamWriter(options.Output.FullName);
                ReportWriter.Create(options.Format).Write(report, file);
            }

            return ShouldFail(report, options.FailOn) ? ExitFindings : ExitOk;
        }
        catch (Exception ex) when (ex is AssetLintException or IOException or UnauthorizedAccessException)
        {
            await stderr.WriteLineAsync($"error: {ex.Message}");
            return ExitUsage;
        }
    }

    private static async Task<LintConfig> LoadConfigAsync(Options options, CancellationToken cancellationToken)
    {
        var file = options.Config
            ?? new FileInfo(Path.Combine(options.Manifest.DirectoryName ?? ".", LintConfig.FileName));

        if (options.Config is null && !file.Exists)
        {
            return LintConfig.Default;
        }

        return await LintConfig.LoadAsync(file.FullName, cancellationToken);
    }

    private static bool ColorAllowed(Options options) =>
        !options.NoColor
        && !Console.IsOutputRedirected
        && Environment.GetEnvironmentVariable("NO_COLOR") is null;

    private static bool ShouldFail(ValidationReport report, FailOn failOn) => failOn switch
    {
        FailOn.None => false,
        FailOn.Info => report.HasFindingsAtOrAbove(Severity.Info),
        FailOn.Warning => report.HasFindingsAtOrAbove(Severity.Warning),
        _ => report.HasFindingsAtOrAbove(Severity.Error),
    };
}
