namespace AssetLint.Core.Rules;

/// <summary>Two assets must not share an id (compared case-insensitively).</summary>
public sealed class DuplicateIdRule()
    : RuleBase("duplicate-id", "Asset ids must be unique (case-insensitive).", Severity.Error)
{
    public override IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var seen = new Dictionary<string, AssetEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in context.Manifest.Assets)
        {
            if (!seen.TryAdd(asset.Id, asset))
            {
                yield return Report(asset, $"Id '{asset.Id}' is already used by {seen[asset.Id].Location}.");
            }
        }
    }
}

/// <summary>Different assets pointing at the same file are usually a copy/paste mistake.</summary>
public sealed class DuplicatePathRule()
    : RuleBase("duplicate-path", "Two assets should not reference the same file.", Severity.Warning)
{
    public override IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var seen = new Dictionary<string, AssetEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in context.Manifest.Assets)
        {
            var normalized = PathHelpers.Normalize(asset.Path);
            if (!seen.TryAdd(normalized, asset))
            {
                var first = seen[normalized];
                yield return Report(asset, $"Path '{asset.Path}' is also used by '{first.Id}' ({first.Location}).");
            }
        }
    }
}

/// <summary>Every dependency must name an asset that exists in the manifest.</summary>
public sealed class MissingDependencyRule()
    : RuleBase("missing-dependency", "Dependencies must refer to assets declared in the manifest.", Severity.Error)
{
    public override IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var ids = context.Manifest.Assets.Select(a => a.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in context.Manifest.Assets)
        {
            foreach (var dependency in asset.Dependencies.Where(d => !ids.Contains(d)))
            {
                yield return Report(asset, $"Depends on '{dependency}', which is not declared in this manifest.");
            }
        }
    }
}

/// <summary>Dependency cycles make load order impossible. Found with a depth-first search.</summary>
public sealed class CircularDependencyRule()
    : RuleBase("circular-dependency", "The dependency graph must be acyclic.", Severity.Error)
{
    private enum VisitState
    {
        InProgress,
        Done,
    }

    public override IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        var byId = new Dictionary<string, AssetEntry>(comparer);
        foreach (var asset in context.Manifest.Assets)
        {
            byId.TryAdd(asset.Id, asset);
        }

        var state = new Dictionary<string, VisitState>(comparer);
        var path = new List<string>();
        var findings = new List<Finding>();

        void Visit(AssetEntry asset)
        {
            state[asset.Id] = VisitState.InProgress;
            path.Add(asset.Id);

            foreach (var dependency in asset.Dependencies)
            {
                if (!byId.TryGetValue(dependency, out var target))
                {
                    continue; // reported by missing-dependency
                }

                if (!state.TryGetValue(target.Id, out var seen))
                {
                    Visit(target);
                }
                else if (seen == VisitState.InProgress)
                {
                    var start = path.FindIndex(id => comparer.Equals(id, target.Id));
                    var cycle = string.Join(" -> ", path.Skip(start).Append(target.Id));
                    findings.Add(Report(target, $"Circular dependency: {cycle}."));
                }
            }

            path.RemoveAt(path.Count - 1);
            state[asset.Id] = VisitState.Done;
        }

        foreach (var asset in byId.Values.OrderBy(a => a.Index))
        {
            if (!state.ContainsKey(asset.Id))
            {
                Visit(asset);
            }
        }

        return findings;
    }
}
