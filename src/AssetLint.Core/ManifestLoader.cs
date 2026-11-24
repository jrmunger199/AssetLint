using System.Globalization;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace AssetLint.Core;

public enum ManifestFormat
{
    Json,
    Xml,
}

/// <summary>Raised for problems the user can fix: unreadable or malformed manifests and configs.</summary>
public sealed class AssetLintException : Exception
{
    public AssetLintException(string message) : base(message) { }

    public AssetLintException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Reads asset manifests from JSON or XML into the same in-memory model.</summary>
public static class ManifestLoader
{
    public static async Task<AssetManifest> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var format = DetectFormat(path);
        var content = await File.ReadAllTextAsync(path, cancellationToken);
        return Parse(content, format) with { SourcePath = path };
    }

    public static ManifestFormat DetectFormat(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".json" => ManifestFormat.Json,
            ".xml" => ManifestFormat.Xml,
            var ext => throw new AssetLintException(
                $"Unsupported manifest extension '{ext}'. Use .json or .xml."),
        };

    public static AssetManifest Parse(string content, ManifestFormat format)
    {
        var manifest = format switch
        {
            ManifestFormat.Json => ParseJson(content),
            ManifestFormat.Xml => ParseXml(content),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };

        return manifest with
        {
            Assets = [.. manifest.Assets.Select((asset, index) => asset with { Index = index })],
        };
    }

    private static AssetManifest ParseJson(string content)
    {
        try
        {
            return JsonSerializer.Deserialize(content, AssetLintJsonContext.Default.AssetManifest)
                ?? throw new AssetLintException("Manifest is empty.");
        }
        catch (JsonException ex)
        {
            var where = ex.LineNumber is { } line ? $" (line {line + 1})" : string.Empty;
            throw new AssetLintException($"Invalid JSON manifest{where}: {ex.Message}", ex);
        }
    }

    private static AssetManifest ParseXml(string content)
    {
        try
        {
            // DTD processing is prohibited and no resolver is set, which rules out XXE and entity-expansion attacks.
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var stringReader = new StringReader(content);
            using var xmlReader = XmlReader.Create(stringReader, settings);
            var root = XDocument.Load(xmlReader, LoadOptions.SetLineInfo).Root;

            if (root is null || root.Name.LocalName != "manifest")
            {
                throw new AssetLintException("Invalid XML manifest: root element must be <manifest>.");
            }

            return new AssetManifest
            {
                Name = Required(root, "name"),
                Version = (string?)root.Attribute("version") ?? "0.0.0",
                Assets = [.. root.Elements("asset").Select(ParseXmlAsset)],
            };
        }
        catch (XmlException ex)
        {
            throw new AssetLintException(
                $"Invalid XML manifest (line {ex.LineNumber}): {ex.Message}", ex);
        }
    }

    private static AssetEntry ParseXmlAsset(XElement element) =>
        new()
        {
            Id = Required(element, "id"),
            Path = Required(element, "path"),
            Type = Required(element, "type"),
            SizeBytes = ParseSize(element),
            Tags = [.. element.Elements("tag").Select(t => t.Value.Trim())],
            Dependencies = [.. element.Elements("dependency").Select(d => d.Value.Trim())],
        };

    private static long ParseSize(XElement element)
    {
        var raw = (string?)element.Attribute("sizeBytes");
        if (raw is null)
        {
            return 0;
        }

        return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size)
            ? size
            : throw new AssetLintException(
                $"Invalid XML manifest{Line(element)}: sizeBytes '{raw}' is not a whole number.");
    }

    private static string Required(XElement element, string attribute) =>
        (string?)element.Attribute(attribute)
        ?? throw new AssetLintException(
            $"Invalid XML manifest{Line(element)}: <{element.Name.LocalName}> is missing the '{attribute}' attribute.");

    private static string Line(XElement element) =>
        element is IXmlLineInfo info && info.HasLineInfo() ? $" (line {info.LineNumber})" : string.Empty;
}
