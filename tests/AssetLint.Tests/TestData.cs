using AssetLint.Core;

namespace AssetLint.Tests;

internal static class TestData
{
    public static AssetEntry Asset(
        string id,
        string? path = null,
        string type = "texture",
        long size = 1024,
        params string[] dependencies) =>
        new()
        {
            Id = id,
            Path = path ?? $"files/{id}.bin",
            Type = type,
            SizeBytes = size,
            Dependencies = dependencies,
        };

    public static AssetManifest Manifest(params AssetEntry[] assets) =>
        new()
        {
            Name = "Test Pack",
            Assets = [.. assets.Select((a, i) => a with { Index = i })],
        };

    /// <summary>Finds a path under the repository's <c>samples</c> folder by walking up from the test binary.</summary>
    public static string Sample(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AssetLint.slnx")))
        {
            dir = dir.Parent;
        }

        return dir is null
            ? throw new InvalidOperationException("Could not locate the repository root.")
            : Path.Combine([dir.FullName, "samples", .. parts]);
    }
}
