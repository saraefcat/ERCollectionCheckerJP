using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ERCollectionCheckerJP.Domain.Items;
using ERCollectionCheckerJP.ItemDatabase.DataPacks;

namespace ERCollectionCheckerJP.ItemDatabase.Tests;

public sealed class ReviewedItemDatabaseDataPackLoaderTests
{
    [Fact]
    public async Task LoadAsync_NormalizesLongswordCanonicalAndTwelveAffinities()
    {
        using var fixture = await RuntimePackFixture.CreateAsync();

        var pack = await ReviewedItemDatabaseDataPackLoader.LoadAsync(fixture.Path);
        var resolver = pack.CreateIdentityResolver();
        var expected = new ItemIdentity(ItemKind.Weapon, 2_000_000);

        Assert.True(resolver.TryResolve(expected, out var canonical));
        Assert.Equal(expected, canonical);
        for (uint affinity = 2_000_100; affinity <= 2_001_200; affinity += 100)
        {
            Assert.True(resolver.TryResolve(new ItemIdentity(ItemKind.Weapon, affinity), out canonical));
            Assert.Equal(expected, canonical);
        }

        Assert.False(resolver.TryResolve(new ItemIdentity(ItemKind.Weapon, 999_999_999), out _));
        Assert.False(resolver.TryResolve(new ItemIdentity(ItemKind.Armor, 2_000_000), out _));
        Assert.Equal(ReviewedItemDatabaseDataPackLoader.ExpectedItemCount, pack.Items.Items.Count);
        Assert.Equal(
            ReviewedItemDatabaseDataPackLoader.ExpectedObtainableItemCount,
            pack.Items.Items.Count(static item => !item.IsDataOnly));
        Assert.Equal(
            ReviewedItemDatabaseDataPackLoader.ExpectedDataOnlyItemCount,
            pack.Items.Items.Count(static item => item.IsDataOnly));
        Assert.Contains(pack.Items.Items, static item =>
            item.IsDataOnly && item.NameJa is null && item.NameEn is null);
        Assert.Equal(ReviewedItemDatabaseDataPackLoader.ExpectedAliasCount, pack.Aliases.Aliases.Count);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("self")]
    [InlineData("chain")]
    [InlineData("cycle")]
    public async Task LoadAsync_RejectsInvalidAliasGraph(string mutation)
    {
        using var fixture = await RuntimePackFixture.CreateAsync();
        var aliases = fixture.Aliases.Aliases.ToArray();
        aliases = mutation switch
        {
            "duplicate" => Replace(aliases, 1, aliases[0]),
            "missing" => Replace(aliases, 0, aliases[0] with { CanonicalParamId = 777_777_777 }),
            "self" => Replace(aliases, 0, aliases[0] with
            {
                ObservedParamId = aliases[0].CanonicalParamId,
            }),
            "chain" => Replace(
                Replace(aliases, 0, aliases[0] with { CanonicalParamId = aliases[1].ObservedParamId }),
                1,
                aliases[1] with { CanonicalParamId = 2_000_000 }),
            "cycle" => Replace(
                Replace(aliases, 0, aliases[0] with { CanonicalParamId = aliases[1].ObservedParamId }),
                1,
                aliases[1] with { CanonicalParamId = aliases[0].ObservedParamId }),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation)),
        };
        await fixture.WriteAliasesAndManifestAsync(fixture.Aliases with { Aliases = aliases });

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            ReviewedItemDatabaseDataPackLoader.LoadAsync(fixture.Path));
    }

    [Fact]
    public async Task LoadAsync_RejectsTamperedPayload()
    {
        using var fixture = await RuntimePackFixture.CreateAsync();
        var path = System.IO.Path.Combine(fixture.Path, ReviewedItemDatabaseDataPackLayout.ItemsFileName);
        await File.AppendAllTextAsync(path, " ");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            ReviewedItemDatabaseDataPackLoader.LoadAsync(fixture.Path));
    }

    private static ReviewedRuntimeAlias[] Replace(
        ReviewedRuntimeAlias[] source,
        int index,
        ReviewedRuntimeAlias value)
    {
        var result = source.ToArray();
        result[index] = value;
        return result;
    }

    private sealed class RuntimePackFixture : IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            NewLine = "\n",
            Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
        };

        private RuntimePackFixture(string path, ReviewedRuntimeAliasesFragment aliases)
        {
            Path = path;
            Aliases = aliases;
        }

        public string Path { get; }

        public ReviewedRuntimeAliasesFragment Aliases { get; private set; }

        public static async Task<RuntimePackFixture> CreateAsync()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ERCollectionCheckerJP.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);

            var items = new List<ReviewedRuntimeItem>
            {
                new(
                    "weapon:2000000",
                    ItemKind.Weapon,
                    ItemKind.Weapon,
                    2_000_000,
                    "ロングソード",
                    "Longsword",
                    ContentPack.BaseGame),
            };
            for (uint index = 1; index < ReviewedItemDatabaseDataPackLoader.ExpectedItemCount; index++)
            {
                var paramId = 10_000_000 + index;
                var item = new ReviewedRuntimeItem(
                    $"weapon:{paramId}",
                    ItemKind.Weapon,
                    ItemKind.Weapon,
                    paramId,
                    index == ReviewedItemDatabaseDataPackLoader.ExpectedObtainableItemCount
                        ? null
                        : $"試験{index}",
                    index == ReviewedItemDatabaseDataPackLoader.ExpectedObtainableItemCount
                        ? null
                        : $"Test {index}",
                    index >= ReviewedItemDatabaseDataPackLoader.ExpectedObtainableItemCount
                        ? ContentPack.Unknown
                        : ContentPack.BaseGame);
                if (index >= ReviewedItemDatabaseDataPackLoader.ExpectedObtainableItemCount)
                {
                    item = item with
                    {
                        Obtainability = Obtainability.Unobtainable,
                        ExclusionReason = ExclusionReason.Unobtainable,
                    };
                }

                items.Add(item);
            }

            var aliases = new List<ReviewedRuntimeAlias>();
            for (uint affinity = 2_000_100; affinity <= 2_001_200; affinity += 100)
            {
                aliases.Add(new ReviewedRuntimeAlias(ItemKind.Weapon, affinity, 2_000_000));
            }

            for (uint index = (uint)aliases.Count; index < ReviewedItemDatabaseDataPackLoader.ExpectedAliasCount; index++)
            {
                aliases.Add(new ReviewedRuntimeAlias(ItemKind.Weapon, 90_000_000 + index, 2_000_000));
            }

            var fixture = new RuntimePackFixture(
                path,
                new ReviewedRuntimeAliasesFragment(2, "1.17", "reviewed-items-with-data-only", aliases));
            await fixture.WriteJsonAsync(
                ReviewedItemDatabaseDataPackLayout.ItemsFileName,
                new ReviewedRuntimeItemsFragment(2, "1.17", "reviewed-items-with-data-only", items));
            await fixture.WriteAliasesAndManifestAsync(fixture.Aliases);
            return fixture;
        }

        public async Task WriteAliasesAndManifestAsync(ReviewedRuntimeAliasesFragment aliases)
        {
            Aliases = aliases;
            await WriteJsonAsync(ReviewedItemDatabaseDataPackLayout.AliasesFileName, aliases);
            var files = new List<RuntimeDataPackFile>();
            foreach (var fileName in ReviewedItemDatabaseDataPackLayout.PayloadFileNames)
            {
                var filePath = System.IO.Path.Combine(Path, fileName);
                await using var input = File.OpenRead(filePath);
                files.Add(new RuntimeDataPackFile(
                    fileName,
                    input.Length,
                    Convert.ToHexString(await SHA256.HashDataAsync(input))));
            }

            await WriteJsonAsync(
                ReviewedItemDatabaseDataPackLayout.ManifestFileName,
                new ReviewedItemDatabaseRuntimeManifest(
                    2,
                    "EldenRing",
                    "1.17",
                    "1.17",
                    "test.reviewed-items",
                    "reviewed-items-with-data-only",
                    new string('A', 64),
                    new string('B', 64),
                    new string('C', 64),
                    ["ja", "en"],
                    files));
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }

        private Task WriteJsonAsync<T>(string fileName, T value) => File.WriteAllTextAsync(
            System.IO.Path.Combine(Path, fileName),
            JsonSerializer.Serialize(value, JsonOptions));
    }
}
