using System.CommandLine;

namespace AssetLint.Cli;

/// <summary>Builds the command tree. Kept separate from <c>Program</c> so tests can drive it in-process.</summary>
public static class CliApp
{
    public static Task<int> RunAsync(
        string[] args,
        TextWriter? output = null,
        TextWriter? error = null,
        CancellationToken cancellationToken = default)
    {
        var configuration = new InvocationConfiguration
        {
            Output = output ?? Console.Out,
            Error = error ?? Console.Error,
        };

        return BuildRootCommand().Parse(args).InvokeAsync(configuration, cancellationToken);
    }

    public static RootCommand BuildRootCommand() =>
        new("AssetLint: validate asset manifests before they reach your build pipeline.")
        {
            ValidateCommand.Create(),
            RulesCommand.Create(),
        };
}
