using AssetLint.Core;

namespace AssetLint.Tests;

public class ManifestLoaderTests
{
    [Fact]
    public void Json_manifest_is_parsed_and_indexed()
    {
        const string json = """
            {
              "name": "Pack",
              "version": "2.0.0",
              "assets": [
                { "id": "a.one", "path": "a.png", "type": "texture", "sizeBytes": 10, "tags": ["x"] },
                { "id": "a.two", "path": "b.png", "type": "texture", "dependencies": ["a.one"] }
              ]
            }
            """;

        var manifest = ManifestLoader.Parse(json, ManifestFormat.Json);

        Assert.Equal("Pack", manifest.Name);
        Assert.Equal("2.0.0", manifest.Version);
        Assert.Equal(2, manifest.Assets.Count);
        Assert.Equal(1, manifest.Assets[1].Index);
        Assert.Equal("assets[1]", manifest.Assets[1].Location);
        Assert.Equal(["a.one"], manifest.Assets[1].Dependencies);
    }

    [Fact]
    public void Omitted_json_collections_are_empty_not_null()
    {
        // Regression: System.Text.Json source generation leaves absent init-only properties null.
        const string json = """{ "name": "Pack", "assets": [ { "id": "a.one", "path": "a.png", "type": "texture" } ] }""";

        var manifest = ManifestLoader.Parse(json, ManifestFormat.Json);

        Assert.Equal("0.0.0", manifest.Version);
        Assert.Empty(manifest.Assets[0].Dependencies);
        Assert.Empty(manifest.Assets[0].Tags);
    }

    [Fact]
    public void Json_allows_comments_and_trailing_commas()
    {
        const string json = """
            {
              // comment
              "name": "Pack",
              "assets": [ { "id": "a.one", "path": "a.png", "type": "texture", }, ],
            }
            """;

        Assert.Single(ManifestLoader.Parse(json, ManifestFormat.Json).Assets);
    }

    [Fact]
    public void Xml_manifest_is_parsed()
    {
        const string xml = """
            <manifest name="Pack" version="1.0.0">
              <asset id="a.one" path="a.png" type="texture" sizeBytes="42">
                <tag>hero</tag>
                <dependency>a.two</dependency>
              </asset>
            </manifest>
            """;

        var asset = Assert.Single(ManifestLoader.Parse(xml, ManifestFormat.Xml).Assets);

        Assert.Equal(42, asset.SizeBytes);
        Assert.Equal(["hero"], asset.Tags);
        Assert.Equal(["a.two"], asset.Dependencies);
    }

    [Theory]
    [InlineData("{ \"assets\": [] }")]                                                        // missing required name
    [InlineData("{ \"name\": \"x\", \"assets\": [ { \"id\": \"a\" } ] }")]                    // missing required members
    [InlineData("{ \"name\": \"x\", \"assets\": [ { \"id\": \"a\", \"path\": \"p\", \"type\": \"t\", \"sizeBytes\": \"big\" } ] }")]
    [InlineData("not json at all")]
    public void Malformed_json_raises_a_friendly_error(string json) =>
        Assert.Throws<AssetLintException>(() => ManifestLoader.Parse(json, ManifestFormat.Json));

    [Theory]
    [InlineData("<manifest name=\"x\"><asset id=\"a\" type=\"t\"/></manifest>")]               // missing path
    [InlineData("<manifest name=\"x\"><asset id=\"a\" path=\"p\" type=\"t\" sizeBytes=\"x\"/></manifest>")]
    [InlineData("<other name=\"x\"/>")]
    [InlineData("<manifest name=\"x\">")]
    public void Malformed_xml_raises_a_friendly_error(string xml) =>
        Assert.Throws<AssetLintException>(() => ManifestLoader.Parse(xml, ManifestFormat.Xml));

    [Fact]
    public void Xml_with_a_doctype_is_rejected()
    {
        const string xml = """
            <!DOCTYPE manifest [ <!ENTITY boom "boom"> ]>
            <manifest name="&boom;" />
            """;

        Assert.Throws<AssetLintException>(() => ManifestLoader.Parse(xml, ManifestFormat.Xml));
    }

    [Theory]
    [InlineData("a.json", ManifestFormat.Json)]
    [InlineData("A.XML", ManifestFormat.Xml)]
    public void Format_is_detected_from_the_extension(string path, ManifestFormat expected) =>
        Assert.Equal(expected, ManifestLoader.DetectFormat(path));

    [Fact]
    public void Unknown_extension_is_rejected() =>
        Assert.Throws<AssetLintException>(() => ManifestLoader.DetectFormat("a.yaml"));

    [Fact]
    public async Task Json_and_xml_samples_describe_the_same_assets()
    {
        var ct = TestContext.Current.CancellationToken;
        var json = await ManifestLoader.LoadAsync(TestData.Sample("hero-pack", "clean.manifest.json"), ct);
        var xml = await ManifestLoader.LoadAsync(TestData.Sample("hero-pack", "hero.manifest.xml"), ct);

        Assert.Equal(json.Assets.Select(a => (a.Id, a.Path, a.SizeBytes)), xml.Assets.Select(a => (a.Id, a.Path, a.SizeBytes)));
        Assert.Equal(json.Assets.SelectMany(a => a.Dependencies), xml.Assets.SelectMany(a => a.Dependencies));
    }
}
