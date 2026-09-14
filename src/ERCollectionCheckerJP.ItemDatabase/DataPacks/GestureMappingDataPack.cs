using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ERCollectionCheckerJP.ItemDatabase.DataPacks;

public static class GestureMappingDataPackLayout
{
    public const string ManifestFileName = "manifest.json";
    public const string MappingsFileName = "gesture-mappings.json";
}

public sealed record GestureMappingRuntimeManifest(
    int SchemaVersion,
    string Game,
    string AppVersion,
    string RegulationVersion,
    string DatabaseVersion,
    string Status,
    string SourceGestureParamSha256,
    string SourceCatalogSha256,
    IReadOnlyList<RuntimeDataPackFile> Files);

public sealed record GestureMappingEntry(
    uint SaveGestureId,
    int GestureParamRowId,
    string ItemKey,
    uint ItemParamId);

public sealed record GestureMappingRuntimeFragment(
    int SchemaVersion,
    string GameVersion,
    string Status,
    IReadOnlyList<GestureMappingEntry> Mappings);

public sealed record GestureMappingDataPack(
    GestureMappingRuntimeManifest Manifest,
    GestureMappingRuntimeFragment GestureMappings);

public sealed record GestureMappingDataPackExpectation(
    string SourceGestureParamSha256,
    string SourceCatalogSha256,
    int MappingCount,
    int ItemCount)
{
    public static GestureMappingDataPackExpectation PrimaryV117 { get; } = new(
        "4A2BA631A53B566F3FC3A38B9A49C970DAE89031AE23D67E0E69E32501245414",
        "CB355F2B47AFA6C11AD1504A8B448167F9E3BFAAB8D8452E35B2BB7B7F2B84EF",
        54,
        53);
}

public static class GestureMappingDataPackLoader
{
    private const int ExpectedSchemaVersion = 1;
    private const string ExpectedGameVersion = "1.17";
    private const string ExpectedStatus = "reviewed-gesture-mappings";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static Task<GestureMappingDataPack> LoadPrimaryAsync(
        string packDirectory,
        CancellationToken cancellationToken = default) => LoadAsync(
            packDirectory,
            GestureMappingDataPackExpectation.PrimaryV117,
            cancellationToken);

    public static async Task<GestureMappingDataPack> LoadAsync(
        string packDirectory,
        GestureMappingDataPackExpectation expectation,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packDirectory);
        ArgumentNullException.ThrowIfNull(expectation);
        var root = Path.GetFullPath(packDirectory);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Gesture mapping Runtime pack was not found: {root}");
        }

        var manifest = await ReadJsonAsync<GestureMappingRuntimeManifest>(
            Path.Combine(root, GestureMappingDataPackLayout.ManifestFileName),
            "gesture mapping manifest",
            cancellationToken);
        ValidateManifest(manifest, expectation);
        await ValidateFilesAsync(root, manifest, cancellationToken);
        var mappings = await ReadJsonAsync<GestureMappingRuntimeFragment>(
            Path.Combine(root, GestureMappingDataPackLayout.MappingsFileName),
            GestureMappingDataPackLayout.MappingsFileName,
            cancellationToken);
        ValidateMappings(mappings, expectation);
        return new GestureMappingDataPack(manifest, mappings);
    }

    private static void ValidateManifest(
        GestureMappingRuntimeManifest manifest,
        GestureMappingDataPackExpectation expectation)
    {
        if (manifest.SchemaVersion != ExpectedSchemaVersion ||
            !string.Equals(manifest.Game, "EldenRing", StringComparison.Ordinal) ||
            !string.Equals(manifest.AppVersion, ExpectedGameVersion, StringComparison.Ordinal) ||
            !string.Equals(manifest.RegulationVersion, ExpectedGameVersion, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(manifest.DatabaseVersion) ||
            !string.Equals(manifest.Status, ExpectedStatus, StringComparison.Ordinal) ||
            !string.Equals(
                manifest.SourceGestureParamSha256,
                expectation.SourceGestureParamSha256,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                manifest.SourceCatalogSha256,
                expectation.SourceCatalogSha256,
                StringComparison.OrdinalIgnoreCase) ||
            manifest.Files is null ||
            manifest.Files.Count != 1)
        {
            throw new InvalidDataException("Gesture mapping manifest contract is unsupported.");
        }

        ValidateSha256(manifest.SourceGestureParamSha256, "source GestureParam");
        ValidateSha256(manifest.SourceCatalogSha256, "source catalog");
        var file = manifest.Files[0];
        if (!string.Equals(file.Path, GestureMappingDataPackLayout.MappingsFileName, StringComparison.Ordinal) ||
            file.ByteLength < 0)
        {
            throw new InvalidDataException("Gesture mapping manifest payload is invalid.");
        }

        ValidateSha256(file.Sha256, file.Path);
    }

    private static async Task ValidateFilesAsync(
        string root,
        GestureMappingRuntimeManifest manifest,
        CancellationToken cancellationToken)
    {
        var file = manifest.Files[0];
        var payloadPath = ResolveContainedPath(root, file.Path);
        if (!File.Exists(payloadPath) || new FileInfo(payloadPath).Length != file.ByteLength)
        {
            throw new InvalidDataException("Gesture mapping Runtime payload is missing or has the wrong length.");
        }

        await using (var input = OpenRead(payloadPath))
        {
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken));
            if (!string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Gesture mapping Runtime payload SHA-256 mismatch.");
            }
        }

        var expectedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            GestureMappingDataPackLayout.ManifestFileName,
            GestureMappingDataPackLayout.MappingsFileName,
        };
        var actualFiles = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!expectedFiles.SetEquals(actualFiles))
        {
            throw new InvalidDataException("Gesture mapping Runtime pack contains an unlisted or missing file.");
        }
    }

    private static void ValidateMappings(
        GestureMappingRuntimeFragment fragment,
        GestureMappingDataPackExpectation expectation)
    {
        if (fragment.SchemaVersion != ExpectedSchemaVersion ||
            !string.Equals(fragment.GameVersion, ExpectedGameVersion, StringComparison.Ordinal) ||
            !string.Equals(fragment.Status, ExpectedStatus, StringComparison.Ordinal) ||
            fragment.Mappings is null ||
            fragment.Mappings.Count != expectation.MappingCount)
        {
            throw new InvalidDataException("Gesture mapping Runtime fragment header or count is invalid.");
        }

        var saveIds = new HashSet<uint>();
        var itemKeys = new HashSet<string>(StringComparer.Ordinal);
        var itemIds = new HashSet<uint>();
        var previousSaveId = 0u;
        foreach (var entry in fragment.Mappings)
        {
            if (entry.GestureParamRowId < 0 || entry.GestureParamRowId > int.MaxValue / 2)
            {
                throw new InvalidDataException($"Invalid GestureParam row ID: {entry.GestureParamRowId}");
            }

            var expectedSaveId = checked((uint)((entry.GestureParamRowId * 2) + 1));
            if (entry.SaveGestureId != expectedSaveId ||
                entry.SaveGestureId <= previousSaveId ||
                entry.ItemParamId == 0 ||
                !string.Equals(entry.ItemKey, $"gesture:{entry.ItemParamId}", StringComparison.Ordinal) ||
                !saveIds.Add(entry.SaveGestureId))
            {
                throw new InvalidDataException($"Invalid gesture mapping entry: {entry.SaveGestureId}");
            }

            itemKeys.Add(entry.ItemKey);
            itemIds.Add(entry.ItemParamId);
            previousSaveId = entry.SaveGestureId;
        }

        if (itemKeys.Count != expectation.ItemCount ||
            itemIds.Count != expectation.ItemCount ||
            !itemKeys.SetEquals(itemIds.Select(static id => $"gesture:{id}")))
        {
            throw new InvalidDataException("Gesture mapping item coverage is invalid.");
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
            throw new InvalidDataException("Gesture mapping manifest paths must be relative.");
        }

        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        var prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Gesture mapping manifest path escapes the pack directory.");
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
