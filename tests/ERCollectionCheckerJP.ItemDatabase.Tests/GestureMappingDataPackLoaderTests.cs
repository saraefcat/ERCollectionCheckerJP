using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using ERCollectionCheckerJP.ItemDatabase.DataPacks;

namespace ERCollectionCheckerJP.ItemDatabase.Tests;

public sealed class GestureMappingDataPackLoaderTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NewLine = "\n",
    };

    [Fact]
    public async Task LoadAsync_AcceptsValidatedPack()
    {
        using var directory = new TemporaryDirectory();
        var expectation = Expectation();
        await WritePackAsync(directory.Path, expectation, Mappings());

        var result = await GestureMappingDataPackLoader.LoadAsync(directory.Path, expectation);

        Assert.Equal(2, result.GestureMappings.Mappings.Count);
        Assert.Equal([1u, 41u], result.GestureMappings.Mappings.Select(static entry => entry.SaveGestureId));
    }

    [Fact]
    public async Task LoadAsync_RejectsSaveIdThatDoesNotMatchGestureParamRow()
    {
        using var directory = new TemporaryDirectory();
        var expectation = Expectation();
        var mappings = Mappings();
        mappings[1] = mappings[1] with { SaveGestureId = 43 };
        await WritePackAsync(directory.Path, expectation, mappings);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            GestureMappingDataPackLoader.LoadAsync(directory.Path, expectation));
    }

    private static GestureMappingDataPackExpectation Expectation() => new(
        new string('A', 64),
        new string('B', 64),
        2,
        2);

    private static GestureMappingEntry[] Mappings() =>
    [
        new GestureMappingEntry(1, 0, "gesture:9000", 9000),
        new GestureMappingEntry(41, 20, "gesture:9011", 9011),
    ];

    private static async Task WritePackAsync(
        string directory,
        GestureMappingDataPackExpectation expectation,
        IReadOnlyList<GestureMappingEntry> mappings)
    {
        var fragment = new GestureMappingRuntimeFragment(
            1,
            "1.17",
            "reviewed-gesture-mappings",
            mappings);
        var payloadPath = Path.Combine(directory, GestureMappingDataPackLayout.MappingsFileName);
        await File.WriteAllTextAsync(payloadPath, JsonSerializer.Serialize(fragment, JsonOptions));
        var payload = await File.ReadAllBytesAsync(payloadPath);
        var manifest = new GestureMappingRuntimeManifest(
            1,
            "EldenRing",
            "1.17",
            "1.17",
            "test.gestures",
            "reviewed-gesture-mappings",
            expectation.SourceGestureParamSha256,
            expectation.SourceCatalogSha256,
            [new RuntimeDataPackFile(
                GestureMappingDataPackLayout.MappingsFileName,
                payload.LongLength,
                Convert.ToHexString(SHA256.HashData(payload)))]);
        await File.WriteAllTextAsync(
            Path.Combine(directory, GestureMappingDataPackLayout.ManifestFileName),
            JsonSerializer.Serialize(manifest, JsonOptions));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ERCollectionCheckerJP.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
