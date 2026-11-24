using System.Text.Json.Serialization;

namespace AssetLint.Core;

/// <summary>
/// Source-generated JSON metadata: no reflection at runtime, so the library stays trimmer/AOT friendly.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    RespectNullableAnnotations = true,
    WriteIndented = true)]
[JsonSerializable(typeof(AssetManifest))]
[JsonSerializable(typeof(LintConfig))]
[JsonSerializable(typeof(ValidationReport))]
internal sealed partial class AssetLintJsonContext : JsonSerializerContext;
