using System.CommandLine;
using AssetLint.Core.Rules;

namespace AssetLint.Cli;

internal static class RulesCommand
{
    public static Command Create()
    {
        var command = new Command("rules", "List the built-in rules and their default severity.");

        command.SetAction(parseResult =>
        {
            var output = parseResult.InvocationConfiguration.Output;
            var idWidth = RuleCatalog.BuiltIn.Max(r => r.Id.Length);

            foreach (var rule in RuleCatalog.BuiltIn)
            {
                var severity = rule.DefaultSeverity.ToString().ToLowerInvariant().PadRight(7);
                output.WriteLine($"{rule.Id.PadRight(idWidth)}  {severity}  {rule.Description}");
            }

            return 0;
        });

        return command;
    }
}
