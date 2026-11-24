namespace AssetLint.Core.Rules;

/// <summary>The rules that ship with AssetLint. A static list keeps discovery reflection-free.</summary>
public static class RuleCatalog
{
    public static IReadOnlyList<IRule> BuiltIn { get; } =
    [
        new DuplicateIdRule(),
        new DuplicatePathRule(),
        new MissingDependencyRule(),
        new CircularDependencyRule(),
        new NamingConventionRule(),
        new UnknownTypeRule(),
        new MaxSizeRule(),
        new PathStyleRule(),
        new PathTraversalRule(),
        new FileExistsRule(),
        new SizeMismatchRule(),
    ];
}
