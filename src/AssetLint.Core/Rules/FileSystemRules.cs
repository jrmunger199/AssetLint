using System.Text.RegularExpressions;

namespace AssetLint.Core.Rules;

/// <summary>Paths must stay inside the asset root: no absolute paths, drive letters or <c>..</c> segments.</summary>
public sealed class PathTraversalRule()
    : RuleBase("path-traversal", "Paths must be relative and stay inside the asset root.", Severity.Error)
{
    public override IEnumerable<Finding> Evaluate(RuleContext context)
    {
        foreach (var asset in context.Manifest.Assets.Where(a => !PathHelpers.IsSafeRelative(a.Path)))
        {
            yield return Report(asset, $"Path '{asset.Path}' is absolute or escapes the asset root.");
        }
    }
}

/// <summary>With a root directory supplied, every asset must exist on disk.</summary>
public sealed class FileExistsRule()
    : RuleBase("file-exists", "Declared files must exist under the asset root (needs --root).", Severity.Error)
{
    public override IEnumerable<Finding> Evaluate(RuleContext context)
    {
        if (context.RootDirectory is not { } root)
        {
            yield break;
        }

        foreach (var asset in context.Manifest.Assets)
        {
            if (PathHelpers.TryResolve(root, asset.Path, out var full) && !File.Exists(full))
            {
                yield return Report(asset, $"File '{asset.Path}' was not found under '{root}'.");
            }
        }
    }
}

/// <summary>With a root directory supplied, declared sizes should match the files on disk.</summary>
public sealed class SizeMismatchRule()
    : RuleBase("size-mismatch", "Declared size should match the file on disk (needs --root).", Severity.Warning)
{
    public override IEnumerable<Finding> Evaluate(RuleContext context)
    {
        if (context.RootDirectory is not { } root)
        {
            yield break;
        }

        foreach (var asset in context.Manifest.Assets)
        {
            if (asset.SizeBytes > 0
                && PathHelpers.TryResolve(root, asset.Path, out var full)
                && File.Exists(full))
            {
                var actual = new FileInfo(full).Length;
                if (actual != asset.SizeBytes)
                {
                    yield return Report(asset, $"Declared size is {asset.SizeBytes} bytes but the file is {actual} bytes.");
                }
            }
        }
    }
}

internal static partial class PathHelpers
{
    [GeneratedRegex(@"^[A-Za-z]:")]
    private static partial Regex DriveLetter();

    public static string Normalize(string path) => path.Trim().Replace('\\', '/');

    public static bool IsSafeRelative(string path)
    {
        var normalized = Normalize(path);
        return normalized.Length > 0
            && !normalized.StartsWith('/')
            && !DriveLetter().IsMatch(normalized)
            && !normalized.Split('/').Contains("..");
    }

    /// <summary>Resolves a manifest path under <paramref name="root"/>; false when it is unsafe.</summary>
    public static bool TryResolve(string root, string path, out string fullPath)
    {
        fullPath = string.Empty;
        if (!IsSafeRelative(path))
        {
            return false;
        }

        var rootFull = Path.GetFullPath(root);
        var candidate = Path.GetFullPath(Path.Combine(rootFull, Normalize(path)));
        var prefix = rootFull.EndsWith(Path.DirectorySeparatorChar) ? rootFull : rootFull + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        fullPath = candidate;
        return true;
    }
}
