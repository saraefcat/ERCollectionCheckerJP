using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using ERCollectionCheckerJP.ItemDatabase.DataPacks;

namespace ERCollectionCheckerJP.ItemDatabase.Tests;

public sealed class TarnishedPackDataPackLoaderTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    [Fact]
    public async Task LoadAsync_AcceptsReviewedSixFragmentPack()
    {
        using var fixture = await RuntimePackFixture.CreateAsync();

        var pack = await TarnishedPackDataPackLoader.LoadAsync(fixture.DirectoryPath);

        Assert.Equal(29, pack.Items.Items.Count);
        Assert.Equal(80, pack.CanonicalAliases.Aliases.Count);
        Assert.Equal(3, pack.EventFlagAddresses.Flags.Count);
        Assert.Equal(["ja", "en"], pack.Manifest.Languages);
    }

    [Fact]
    public async Task LoadAsync_RejectsPayloadHashMismatch()
    {
        using var fixture = await RuntimePackFixture.CreateAsync();
        var path = fixture.PayloadPath(TarnishedPackDataPackLayout.ItemsFileName);
        var bytes = await File.ReadAllBytesAsync(path);
        bytes[^2] = bytes[^2] == (byte)' ' ? (byte)'\t' : (byte)' ';
        await File.WriteAllBytesAsync(path, bytes);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            TarnishedPackDataPackLoader.LoadAsync(fixture.DirectoryPath));
    }

    [Fact]
    public async Task LoadAsync_RejectsPayloadLengthMismatch()
    {
        using var fixture = await RuntimePackFixture.CreateAsync();
        var manifest = fixture.Manifest with
        {
            Files = fixture.Manifest.Files
                .Select(file => file.Path == TarnishedPackDataPackLayout.ItemsFileName
                    ? file with { ByteLength = file.ByteLength + 1 }
                    : file)
                .ToArray(),
        };
        await fixture.WriteManifestAsync(manifest);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            TarnishedPackDataPackLoader.LoadAsync(fixture.DirectoryPath));
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("language")]
    [InlineData("version")]
    public async Task LoadAsync_RejectsUnsupportedManifestContract(string mutation)
    {
        using var fixture = await RuntimePackFixture.CreateAsync();
        var manifest = mutation switch
        {
            "schema" => fixture.Manifest with { SchemaVersion = 2 },
            "language" => fixture.Manifest with { Languages = ["ja", "fr"] },
            "version" => fixture.Manifest with { AppVersion = "1.18" },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation)),
        };
        await fixture.WriteManifestAsync(manifest);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            TarnishedPackDataPackLoader.LoadAsync(fixture.DirectoryPath));
    }

    [Fact]
    public async Task LoadAsync_RejectsUnlistedJsonPayload()
    {
        using var fixture = await RuntimePackFixture.CreateAsync();
        await File.WriteAllTextAsync(
            Path.Combine(fixture.DirectoryPath, "unlisted.json"),
            "{\"schemaVersion\":1}");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            TarnishedPackDataPackLoader.LoadAsync(fixture.DirectoryPath));
    }

    [Fact]
    public async Task LoadAsync_RejectsUnlistedNonJsonPayload()
    {
        using var fixture = await RuntimePackFixture.CreateAsync();
        await File.WriteAllBytesAsync(
            Path.Combine(fixture.DirectoryPath, "unexpected.dll"),
            [0x4D, 0x5A]);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            TarnishedPackDataPackLoader.LoadAsync(fixture.DirectoryPath));
    }

    private sealed class RuntimePackFixture : IDisposable
    {
        private RuntimePackFixture(string directoryPath, RuntimeDataPackManifest manifest)
        {
            DirectoryPath = directoryPath;
            Manifest = manifest;
        }

        public string DirectoryPath { get; }

        public RuntimeDataPackManifest Manifest { get; }

        public static async Task<RuntimePackFixture> CreateAsync()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "ERCollectionCheckerJP.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var source = Path.Combine(AppContext.BaseDirectory, "TestData", "fragments");
            foreach (var fileName in TarnishedPackDataPackLayout.PayloadFileNames)
            {
                File.Copy(Path.Combine(source, fileName), Path.Combine(directory, fileName));
            }

            var files = new List<RuntimeDataPackFile>();
            foreach (var fileName in TarnishedPackDataPackLayout.PayloadFileNames)
            {
                var path = Path.Combine(directory, fileName);
                await using var stream = File.OpenRead(path);
                files.Add(new RuntimeDataPackFile(
                    fileName,
                    stream.Length,
                    Convert.ToHexString(await SHA256.HashDataAsync(stream))));
            }

            var manifest = new RuntimeDataPackManifest(
                1,
                "EldenRing",
                "1.17",
                "1.17",
                "test.1.17",
                "reviewed-fragment",
                new string('A', 64),
                ["ja", "en"],
                files);
            var fixture = new RuntimePackFixture(directory, manifest);
            await fixture.WriteManifestAsync(manifest);
            return fixture;
        }

        public string PayloadPath(string fileName) => Path.Combine(DirectoryPath, fileName);

        public Task WriteManifestAsync(RuntimeDataPackManifest manifest) => File.WriteAllTextAsync(
            Path.Combine(DirectoryPath, TarnishedPackDataPackLayout.ManifestFileName),
            JsonSerializer.Serialize(manifest, JsonOptions));

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }
}
