using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using ERCollectionCheckerJP.Domain.Items;

namespace ERCollectionCheckerJP.ItemDatabase.DataPacks;

public static class GoodsClassificationDataPackLayout
{
    public const string ManifestFileName = "manifest.json";
    public const string ClassificationsFileName = "goods-classifications.json";
}

public static class GoodsClassificationRules
{
    public static GoodsCategory FromGoodsType(int goodsType) => goodsType switch
    {
        0 => GoodsCategory.General,
        1 => GoodsCategory.KeyItem,
        2 => GoodsCategory.Material,
        3 => GoodsCategory.Remembrance,
        7 or 8 => GoodsCategory.SpiritAsh,
        9 => GoodsCategory.Flask,
        10 => GoodsCategory.CrystalTear,
        11 => GoodsCategory.ReusableContainer,
        12 => GoodsCategory.Information,
        14 => GoodsCategory.UpgradeMaterial,
        _ => throw new InvalidDataException($"Unsupported EquipParamGoods goodsType: {goodsType}"),
    };

    public static bool IsReviewedCollectionCategory(GoodsCategory category) =>
        category is GoodsCategory.SpiritAsh or GoodsCategory.CrystalTear;
}

public sealed record GoodsClassificationRuntimeManifest(
    int SchemaVersion,
    string Game,
    string AppVersion,
    string RegulationVersion,
    string DatabaseVersion,
    string Status,
    string SourceCatalogSha256,
    IReadOnlyList<RuntimeDataPackFile> Files);

public sealed record GoodsClassificationEntry(
    string ItemKey,
    uint ItemParamId,
    int GoodsType,
    GoodsCategory Category,
    int SortId,
    bool IsDeposit);

public sealed record GoodsClassificationRuntimeFragment(
    int SchemaVersion,
    string GameVersion,
    string Status,
    IReadOnlyList<GoodsClassificationEntry> Entries);

public sealed record GoodsClassificationDataPack(
    GoodsClassificationRuntimeManifest Manifest,
    GoodsClassificationRuntimeFragment Classifications);

public sealed record GoodsClassificationDataPackExpectation(
    string SourceCatalogSha256,
    int ItemCount,
    IReadOnlyDictionary<GoodsCategory, int> CategoryCounts)
{
    public static GoodsClassificationDataPackExpectation PrimaryV117 { get; } = new(
        "CB355F2B47AFA6C11AD1504A8B448167F9E3BFAAB8D8452E35B2BB7B7F2B84EF",
        952,
        new Dictionary<GoodsCategory, int>
        {
            [GoodsCategory.General] = 261,
            [GoodsCategory.KeyItem] = 285,
            [GoodsCategory.Material] = 106,
            [GoodsCategory.Remembrance] = 25,
            [GoodsCategory.SpiritAsh] = 84,
            [GoodsCategory.Flask] = 2,
            [GoodsCategory.CrystalTear] = 40,
            [GoodsCategory.ReusableContainer] = 4,
            [GoodsCategory.Information] = 102,
            [GoodsCategory.UpgradeMaterial] = 43,
        });
}

public static class GoodsClassificationDataPackLoader
{
    private const int ExpectedSchemaVersion = 1;
    private const string ExpectedGameVersion = "1.17";
    private const string ExpectedStatus = "reviewed-goods-classifications";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    public static Task<GoodsClassificationDataPack> LoadPrimaryAsync(
        string packDirectory,
        CancellationToken cancellationToken = default) => LoadAsync(
            packDirectory,
            GoodsClassificationDataPackExpectation.PrimaryV117,
            cancellationToken);

    public static async Task<GoodsClassificationDataPack> LoadAsync(
        string packDirectory,
        GoodsClassificationDataPackExpectation expectation,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packDirectory);
        ArgumentNullException.ThrowIfNull(expectation);
        var root = Path.GetFullPath(packDirectory);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Goods classification Runtime pack was not found: {root}");
        }

        var manifest = await ReadJsonAsync<GoodsClassificationRuntimeManifest>(
            Path.Combine(root, GoodsClassificationDataPackLayout.ManifestFileName),
            "goods classification manifest",
            cancellationToken);
        ValidateManifest(manifest, expectation);
        await ValidateFilesAsync(root, manifest, cancellationToken);
        var classifications = await ReadJsonAsync<GoodsClassificationRuntimeFragment>(
            Path.Combine(root, GoodsClassificationDataPackLayout.ClassificationsFileName),
            GoodsClassificationDataPackLayout.ClassificationsFileName,
            cancellationToken);
        ValidateClassifications(classifications, expectation);
        return new GoodsClassificationDataPack(manifest, classifications);
    }

    private static void ValidateManifest(
        GoodsClassificationRuntimeManifest manifest,
        GoodsClassificationDataPackExpectation expectation)
    {
        if (manifest.SchemaVersion != ExpectedSchemaVersion ||
            !string.Equals(manifest.Game, "EldenRing", StringComparison.Ordinal) ||
            !string.Equals(manifest.AppVersion, ExpectedGameVersion, StringComparison.Ordinal) ||
            !string.Equals(manifest.RegulationVersion, ExpectedGameVersion, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(manifest.DatabaseVersion) ||
            !string.Equals(manifest.Status, ExpectedStatus, StringComparison.Ordinal) ||
            !string.Equals(
                manifest.SourceCatalogSha256,
                expectation.SourceCatalogSha256,
                StringComparison.OrdinalIgnoreCase) ||
            manifest.Files is null ||
            manifest.Files.Count != 1)
        {
            throw new InvalidDataException("Goods classification manifest contract is unsupported.");
        }

        ValidateSha256(manifest.SourceCatalogSha256, "source catalog");
        var file = manifest.Files[0];
        if (!string.Equals(
                file.Path,
                GoodsClassificationDataPackLayout.ClassificationsFileName,
                StringComparison.Ordinal) ||
            file.ByteLength < 0)
        {
            throw new InvalidDataException("Goods classification manifest payload is invalid.");
        }

        ValidateSha256(file.Sha256, file.Path);
    }

    private static async Task ValidateFilesAsync(
        string root,
        GoodsClassificationRuntimeManifest manifest,
        CancellationToken cancellationToken)
    {
        var file = manifest.Files[0];
        var payloadPath = ResolveContainedPath(root, file.Path);
        if (!File.Exists(payloadPath) || new FileInfo(payloadPath).Length != file.ByteLength)
        {
            throw new InvalidDataException(
                "Goods classification Runtime payload is missing or has the wrong length.");
        }

        await using (var input = OpenRead(payloadPath))
        {
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken));
            if (!string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Goods classification Runtime payload SHA-256 mismatch.");
            }
        }

        var expectedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            GoodsClassificationDataPackLayout.ManifestFileName,
            GoodsClassificationDataPackLayout.ClassificationsFileName,
        };
        var actualFiles = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!expectedFiles.SetEquals(actualFiles))
        {
            throw new InvalidDataException(
                "Goods classification Runtime pack contains an unlisted or missing file.");
        }
    }

    private static void ValidateClassifications(
        GoodsClassificationRuntimeFragment fragment,
        GoodsClassificationDataPackExpectation expectation)
    {
        if (fragment.SchemaVersion != ExpectedSchemaVersion ||
            !string.Equals(fragment.GameVersion, ExpectedGameVersion, StringComparison.Ordinal) ||
            !string.Equals(fragment.Status, ExpectedStatus, StringComparison.Ordinal) ||
            fragment.Entries is null ||
            fragment.Entries.Count != expectation.ItemCount)
        {
            throw new InvalidDataException("Goods classification Runtime fragment header or count is invalid.");
        }

        var previousParamId = 0u;
        var categoryCounts = new Dictionary<GoodsCategory, int>();
        foreach (var entry in fragment.Entries)
        {
            GoodsCategory expectedCategory;
            try
            {
                expectedCategory = GoodsClassificationRules.FromGoodsType(entry.GoodsType);
            }
            catch (InvalidDataException exception)
            {
                throw new InvalidDataException(
                    $"Invalid goods classification entry: {entry.ItemKey}",
                    exception);
            }

            if (entry.ItemParamId == 0 ||
                entry.ItemParamId <= previousParamId ||
                !string.Equals(entry.ItemKey, $"goods:{entry.ItemParamId}", StringComparison.Ordinal) ||
                entry.Category != expectedCategory ||
                entry.SortId < 0)
            {
                throw new InvalidDataException($"Invalid goods classification entry: {entry.ItemKey}");
            }

            categoryCounts[entry.Category] = categoryCounts.GetValueOrDefault(entry.Category) + 1;
            previousParamId = entry.ItemParamId;
        }

        if (categoryCounts.Count != expectation.CategoryCounts.Count ||
            expectation.CategoryCounts.Any(expected =>
                categoryCounts.GetValueOrDefault(expected.Key) != expected.Value))
        {
            throw new InvalidDataException("Goods classification category counts are invalid.");
        }
    }

    private static async Task<T> ReadJsonAsync<T>(
        string path,
        string description,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"Missing {description}: {path}");
        }

        try
        {
            await using var input = OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(input, JsonOptions, cancellationToken) ??
                throw new InvalidDataException($"Empty {description}: {path}");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Invalid {description}: {path}", exception);
        }
    }

    private static string ResolveContainedPath(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException("Goods classification manifest paths must be relative.");
        }

        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        var prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Goods classification manifest path escapes the pack directory.");
        }

        return fullPath;
    }

    private static void ValidateSha256(string value, string source)
    {
        if (value.Length != 64 || value.Any(static character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException($"Invalid SHA-256 for {source}.");
        }
    }

    private static FileStream OpenRead(string path) => new(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        64 * 1024,
        FileOptions.Asynchronous | FileOptions.SequentialScan);
}
