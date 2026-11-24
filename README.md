# AssetLint

[![CI](https://github.com/jrmunger199/AssetLint/actions/workflows/ci.yml/badge.svg)](https://github.com/jrmunger199/AssetLint/actions/workflows/ci.yml)
![.NET 10](https://img.shields.io/badge/.NET-10%20LTS-512BD4)
![C# 14](https://img.shields.io/badge/C%23-14-239120)
![License: MIT](https://img.shields.io/badge/license-MIT-blue)

A fast, extensible validator for asset manifests, written in modern C#. It catches broken
references, dependency cycles, naming and size violations, and missing files **before** they
reach your build pipeline, and reports them as text, JSON, or SARIF for GitHub code scanning.

Teams that ship content pipelines (games, apps, media) usually keep a manifest listing every
asset, where it lives, and what it depends on. When that file is wrong, the failure shows up late
and far from the cause: a broken build, a missing texture at runtime, a load-order crash.
AssetLint moves that failure to the commit that caused it.

```text
$ assetlint validate samples/hero-pack/broken.manifest.json

Hero Pack (broken) (8 assets)

  error   circular-dependency  assets[2] Circular dependency: mesh.hero.body -> mat.hero.main -> mesh.hero.body.
  error   duplicate-id         assets[1] Id 'TEX.HERO.DIFFUSE' is already used by assets[0].
  error   max-size             assets[6] Declared size -1 is negative.
  error   missing-dependency   assets[3] Depends on 'tex.hero.normal', which is not declared in this manifest.
  error   path-traversal       assets[6] Path '../secrets.txt' is absolute or escapes the asset root.
  warning duplicate-path       assets[7] Path 'textures/hero_diffuse.png' is also used by 'tex.hero.diffuse' (assets[0]).
  warning max-size             assets[5] Size 40 MiB exceeds the 16 MiB budget for 'texture' assets.
  warning naming-convention    assets[1] Id 'TEX.HERO.DIFFUSE' does not match the pattern '^[a-z0-9]+(\.[a-z0-9_]+)+$'.
  warning naming-convention    assets[4] Id 'HeroTrailFx' does not match the pattern '^[a-z0-9]+(\.[a-z0-9_]+)+$'.
  warning path-style           assets[2] Path 'meshes\hero_body.fbx' uses backslashes; use forward slashes.
  warning unknown-type         assets[4] Type 'particles' is not recognised. Known types: texture, mesh, audio, shader, material, animation, script, data.

5 errors, 6 warnings, 0 info
```

## Quick start

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/jrmunger199/AssetLint.git
cd AssetLint

# Validate a manifest
dotnet run --project src/AssetLint.Cli -- validate samples/hero-pack/clean.manifest.json

# Also check that files exist on disk and sizes match
dotnet run --project src/AssetLint.Cli -- validate samples/hero-pack/clean.manifest.json --root samples/hero-pack/assets

# Install as a global tool from a local build
dotnet pack src/AssetLint.Cli -c Release -o artifacts
dotnet tool install --global --add-source artifacts AssetLint
assetlint rules
```

### Commands

| Command | Purpose |
| --- | --- |
| `assetlint validate <manifest>` | Validate a `.json` or `.xml` manifest. |
| `assetlint rules` | List the built-in rules and their default severities. |

`validate` options:

| Option | Default | Description |
| --- | --- | --- |
| `--root <dir>` | none | Asset root. Enables the on-disk rules (`file-exists`, `size-mismatch`). |
| `-c, --config <file>` | `assetlint.json` beside the manifest | Rule and threshold configuration. |
| `-f, --format <text\|json\|sarif>` | `text` | Report format. |
| `-o, --output <file>` | stdout | Write the report to a file. |
| `--fail-on <none\|info\|warning\|error>` | `error` | Lowest severity that produces exit code 1. |
| `--no-color` | off | Disable ANSI colours (`NO_COLOR` is also honoured). |

**Exit codes:** `0` passed, `1` findings at or above `--fail-on`, `2` usage or I/O error (bad path, malformed manifest or config).
That makes it easy to drop into CI or a pre-commit hook.

## Manifest formats

JSON (comments and trailing commas allowed) and XML describe the same model:

```json
{
  "name": "Hero Pack",
  "version": "1.2.0",
  "assets": [
    { "id": "tex.hero.diffuse", "path": "textures/hero_diffuse.png", "type": "texture", "sizeBytes": 2048 },
    { "id": "mat.hero.main", "path": "materials/hero_main.mat", "type": "material",
      "sizeBytes": 512, "dependencies": ["tex.hero.diffuse"] }
  ]
}
```

```xml
<manifest name="Hero Pack" version="1.2.0">
  <asset id="mat.hero.main" path="materials/hero_main.mat" type="material" sizeBytes="512">
    <dependency>tex.hero.diffuse</dependency>
  </asset>
</manifest>
```

## Rules

| Rule | Default | Checks |
| --- | --- | --- |
| `duplicate-id` | error | Asset ids are unique (case-insensitive). |
| `duplicate-path` | warning | Two assets do not point at the same file. |
| `missing-dependency` | error | Every dependency names a declared asset. |
| `circular-dependency` | error | The dependency graph is acyclic; the full cycle is reported. |
| `naming-convention` | warning | Ids match the configured regex. |
| `unknown-type` | warning | Type is one of the configured known types. |
| `max-size` | warning | Size is within the per-type budget (negative sizes are errors). |
| `path-style` | warning | Forward slashes, no stray whitespace. |
| `path-traversal` | error | Paths are relative and cannot escape the asset root. |
| `file-exists` | error | Files exist on disk (needs `--root`). |
| `size-mismatch` | warning | Declared size matches the file on disk (needs `--root`). |

### Configuration

Drop an `assetlint.json` next to your manifest (or pass `--config`). Every key is optional; anything you
omit keeps its default. See [`samples/custom-config`](samples/custom-config).

```json
{
  "namingPattern": "^[A-Z][A-Za-z0-9]+$",
  "knownTypes": ["texture", "mesh", "sound"],
  "maxSizeBytes": { "texture": 1048576 },
  "rules": {
    "naming-convention": { "severity": "error" },
    "unknown-type": { "enabled": false }
  }
}
```

## CI and code scanning

`--format sarif` produces SARIF 2.1.0, which GitHub turns into code-scanning alerts. This repository
dogfoods it: see the `code-scanning-demo` job in [`.github/workflows/ci.yml`](.github/workflows/ci.yml).

```yaml
- run: assetlint validate content/manifest.json --root content --format sarif --output assetlint.sarif
- uses: github/codeql-action/upload-sarif@v3
  with:
    sarif_file: assetlint.sarif
```

## Architecture

```text
src/
  AssetLint.Core/            Library: no console, no I/O beyond reading files
    Models.cs                  AssetEntry, AssetManifest, Finding, Severity
    ManifestLoader.cs          JSON + XML  ->  one model
    LintConfig.cs              Optional settings with safe defaults
    Rules/                     IRule, RuleBase, and the 11 built-in rules
    Validator.cs               Runs rules in parallel, applies config, orders results
    Reporting/                 Text, JSON and SARIF writers
  AssetLint.Cli/             Thin System.CommandLine front end (packs as a dotnet tool)
tests/AssetLint.Tests/       69 xUnit v3 tests: rules, loader, validator, reporters, CLI end-to-end
samples/                     Clean, broken, XML and custom-config examples used by docs and tests
```

### Adding a rule

Rules are small, pure classes. Derive from `RuleBase`, return findings, and register the rule:

```csharp
public sealed class NoDataTypeRule() : RuleBase("no-data", "Disallows data assets.", Severity.Warning)
{
    public override IEnumerable<Finding> Evaluate(RuleContext context) =>
        context.Manifest.Assets
            .Where(a => a.Type == "data")
            .Select(a => Report(a, "Data assets are not allowed."));
}

var report = new Validator([.. RuleCatalog.BuiltIn, new NoDataTypeRule()]).Validate(manifest);
```

### Design decisions

- **Core is separate from the CLI.** The engine is a plain library, so the same rules could back an editor
  plugin, a build task, or a service. The CLI is a thin shell and is tested in-process.
- **Rules are independent and read-only,** so the validator runs them in parallel (`AsParallel().AsOrdered()`)
  and sorts the results. Output is deterministic, which keeps CI diffs and snapshots stable.
- **A faulty rule never kills the run.** An exception inside a rule becomes an error finding for that rule.
- **Untrusted input is treated as untrusted.** XML is parsed with DTDs prohibited (no XXE or entity
  expansion), the user-supplied naming regex has a match timeout, and disk rules refuse any path that
  resolves outside `--root`.
- **Source-generated JSON.** `System.Text.Json` source generation means no runtime reflection, which keeps the
  library trimmer-friendly. It surfaced a subtle gotcha: absent properties on `init`-only types can arrive as
  `null` instead of their initializer value. The models use C# 14's `field` keyword to guarantee safe defaults.
  Both cases have regression tests.
- **Warnings are errors** (`TreatWarningsAsErrors` with the latest analyzers), so the codebase stays clean.

## Development

```bash
dotnet build
dotnet test      # xUnit v3 on Microsoft.Testing.Platform (opted in via global.json)
dotnet format whitespace AssetLint.slnx --verify-no-changes
```

CI also fails the build on known-vulnerable dependencies, runs CodeQL, and Dependabot keeps packages current.

## License

[MIT](LICENSE)
