using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ERCollectionCheckerJP.Domain.Items;
using ERCollectionCheckerJP.ItemDatabase.DataPacks;

namespace ERCollectionCheckerJP.ItemDatabase.Tests;

public sealed class GoodsClassificationDataPackLoaderTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NewLine = "\n",
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task LoadAsync_AcceptsValidatedPack()
    {
        using var directory = new TemporaryDirectory();
        var expectation = Expectation();
        await WritePackAsync(directory.Path, expectation, Entries());

        var result = await GoodsClassificationDataPackLoader.LoadAsync(
            directory.Path,
            expectation);

        Assert.Equal(2, result.Classifications.Entries.Count);
        Assert.Equal(
            [GoodsCategory.CrystalTear, GoodsCategory.SpiritAsh],
            result.Classifications.Entries.Select(static entry => entry.Category));
    }

    [Fact]
    public async Task LoadAsync_RejectsCategoryThatDoesNotMatchGoodsType()
    {
        using var directory = new TemporaryDirectory();
        var expectation = Expectation();
        var entries = Entries();
        entries[0] = entries[0] with { Category = GoodsCategory.Material };
        await WritePackAsync(directory.Path, expectation, entries);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            GoodsClassificationDataPackLoader.LoadAsync(directory.Path, expectation));
    }

    private static GoodsClassificationDataPackExpectation Expectation() => new(
        new string('A', 64),
        2,
        new Dictionary<GoodsCategory, int>
        {
            [GoodsCategory.SpiritAsh] = 1,
            [GoodsCategory.CrystalTear] = 1,
        });

    private static GoodsClassificationEntry[] Entries() =>
    [
        new GoodsClassificationEntry("goods:11000", 11_000, 10, GoodsCategory.CrystalTear, 10, false),
        new GoodsClassificationEntry("goods:203000", 203_000, 7, GoodsCategory.SpiritAsh, 20, true),
    ];

    private static async Task WritePackAsync(
        string directory,
        GoodsClassificationDataPackExpectation expectation,
        IReadOnlyList<GoodsClassificationEntry> entries)
    {
        var fragment = new GoodsClassificationRuntimeFragment(
            1,
            "1.17",
            "reviewed-goods-classifications",
            entries);
        var payloadPath = Path.Combine(
            directory,
            GoodsClassificationDataPackLayout.ClassificationsFileName);
        await File.WriteAllTextAsync(payloadPath, JsonSerializer.Serialize(fragment, JsonOptions));
        var payload = await File.ReadAllBytesAsync(payloadPath);
        var manifest = new GoodsClassificationRuntimeManifest(
            1,
            "EldenRing",
            "1.17",
            "1.17",
            "test.goods",
            "reviewed-goods-classifications",
            expectation.SourceCatalogSha256,
            [new RuntimeDataPackFile(
                GoodsClassificationDataPackLayout.ClassificationsFileName,
                payload.LongLength,
                Convert.ToHexString(SHA256.HashData(payload)))]);
        await File.WriteAllTextAsync(
            Path.Combine(directory, GoodsClassificationDataPackLayout.ManifestFileName),
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
