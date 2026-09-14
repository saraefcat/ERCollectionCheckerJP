using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using ERCollectionCheckerJP.Domain.Items;
using ERCollectionCheckerJP.ItemDatabase.Catalog;

namespace ERCollectionCheckerJP.ItemDatabase.DataPacks;

public static class ArmorConversionDataPackLayout
{
    public const string ManifestFileName = "manifest.json";
    public const string ConversionsFileName = "armor-conversions.json";
}

public sealed record ArmorConversionRuntimeManifest(
    int SchemaVersion,
    string Game,
    string AppVersion,
    string RegulationVersion,
    string DatabaseVersion,
    string Status,
    string SourcePackManifestSha256,
    string SourceCatalogSha256,
    IReadOnlyList<RuntimeDataPackFile> Files);

public sealed record ArmorConversionEvidence(
    uint SourceParamId,
    uint TargetParamId,
    IReadOnlyList<int> ShopRowIds,
    IReadOnlyList<int> MaterialSetIds);

public sealed record ArmorConversionRuntimeFragment(
    int SchemaVersion,
    string GameVersion,
    string Status,
    IReadOnlyList<ArmorConversionEntry> Entries,
    IReadOnlyList<ArmorConversionEvidence> Evidence);

public sealed record ArmorConversionDataPack(
    ArmorConversionRuntimeManifest Manifest,
    ArmorConversionRuntimeFragment Conversions);

public sealed record ArmorIndependentPair(uint NormalParamId, uint AlteredParamId);

public sealed record ArmorConversionDataPackExpectation(
    string SourcePackManifestSha256,
    string SourceCatalogSha256,
    int EntryCount,
    int EvidenceCount,
    int DirectedEdgeCount,
    int BidirectionalPairCount,
    int NormalVariantCount,
    int AlteredVariantCount,
    int SpecialVariantCount,
    int DirectOnlyCount,
    int TransformOnlyCount,
    int DirectOrTransformCount,
    IReadOnlyList<ArmorIndependentPair> IndependentPairs)
{
    public static ArmorConversionDataPackExpectation PrimaryV117 { get; } = new(
        "335A011E37F05436486828845699C6ED03994671FE92AC3DD4D44C023CBC5F73",
        "CB355F2B47AFA6C11AD1504A8B448167F9E3BFAAB8D8452E35B2BB7B7F2B84EF",
        722,
        170,
        170,
        85,
        630,
        90,
        2,
        552,
        84,
        86,
        [
            new ArmorIndependentPair(200_000, 201_000),
            new ArmorIndependentPair(200_100, 201_100),
            new ArmorIndependentPair(360_100, 361_100),
            new ArmorIndependentPair(370_100, 371_100),
            new ArmorIndependentPair(800_100, 801_100),
            new ArmorIndependentPair(810_000, 811_000),
        ]);
}

public static class ArmorConversionDataPackLoader
{
    private const int ExpectedSchemaVersion = 1;
    private const string ExpectedGameVersion = "1.17";
    private const string ExpectedStatus = "reviewed-armor-conversions";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    public static Task<ArmorConversionDataPack> LoadPrimaryAsync(
        string packDirectory,
        CancellationToken cancellationToken = default) => LoadAsync(
            packDirectory,
            ArmorConversionDataPackExpectation.PrimaryV117,
            cancellationToken);

    public static async Task<ArmorConversionDataPack> LoadAsync(
        string packDirectory,
        ArmorConversionDataPackExpectation expectation,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packDirectory);
        ArgumentNullException.ThrowIfNull(expectation);
        var root = Path.GetFullPath(packDirectory);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Armor conversion Runtime pack was not found: {root}");
        }

        var manifest = await ReadJsonAsync<ArmorConversionRuntimeManifest>(
            Path.Combine(root, ArmorConversionDataPackLayout.ManifestFileName),
            "armor conversion manifest",
            cancellationToken);
        ValidateManifest(manifest, expectation);
        await ValidateFilesAsync(root, manifest, cancellationToken);
        var conversions = await ReadJsonAsync<ArmorConversionRuntimeFragment>(
            Path.Combine(root, ArmorConversionDataPackLayout.ConversionsFileName),
            ArmorConversionDataPackLayout.ConversionsFileName,
            cancellationToken);
        ValidateConversions(conversions, expectation);
        return new ArmorConversionDataPack(manifest, conversions);
    }

    private static void ValidateManifest(
        ArmorConversionRuntimeManifest manifest,
        ArmorConversionDataPackExpectation expectation)
    {
        if (manifest.SchemaVersion != ExpectedSchemaVersion ||
            !string.Equals(manifest.Game, "EldenRing", StringComparison.Ordinal) ||
            !string.Equals(manifest.AppVersion, ExpectedGameVersion, StringComparison.Ordinal) ||
            !string.Equals(manifest.RegulationVersion, ExpectedGameVersion, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(manifest.DatabaseVersion) ||
            !string.Equals(manifest.Status, ExpectedStatus, StringComparison.Ordinal) ||
            !string.Equals(
                manifest.SourcePackManifestSha256,
                expectation.SourcePackManifestSha256,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                manifest.SourceCatalogSha256,
                expectation.SourceCatalogSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Armor conversion manifest contract is unsupported.");
        }

        ValidateSha256(manifest.SourcePackManifestSha256, "source pack manifest");
        ValidateSha256(manifest.SourceCatalogSha256, "source catalog");
        if (manifest.Files is null || manifest.Files.Count != 1)
        {
            throw new InvalidDataException("Armor conversion manifest must contain one payload.");
        }

        var file = manifest.Files[0];
        if (!string.Equals(
                file.Path,
                ArmorConversionDataPackLayout.ConversionsFileName,
                StringComparison.Ordinal) ||
            file.ByteLength < 0)
        {
            throw new InvalidDataException("Armor conversion manifest payload is invalid.");
        }

        ValidateSha256(file.Sha256, file.Path);
    }

    private static async Task ValidateFilesAsync(
        string root,
        ArmorConversionRuntimeManifest manifest,
        CancellationToken cancellationToken)
    {
        var file = manifest.Files[0];
        var payloadPath = ResolveContainedPath(root, file.Path);
        if (!File.Exists(payloadPath) || new FileInfo(payloadPath).Length != file.ByteLength)
        {
            throw new InvalidDataException("Armor conversion Runtime payload is missing or has the wrong length.");
        }

        await using (var input = OpenRead(payloadPath))
        {
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken));
            if (!string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Armor conversion Runtime payload SHA-256 mismatch.");
            }
        }

        var expectedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ArmorConversionDataPackLayout.ManifestFileName,
            ArmorConversionDataPackLayout.ConversionsFileName,
        };
        var actualFiles = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!expectedFiles.SetEquals(actualFiles))
        {
            throw new InvalidDataException("Armor conversion Runtime pack contains an unlisted or missing file.");
        }
    }

    private static void ValidateConversions(
        ArmorConversionRuntimeFragment conversions,
        ArmorConversionDataPackExpectation expectation)
    {
        if (conversions.SchemaVersion != ExpectedSchemaVersion ||
            !string.Equals(conversions.GameVersion, ExpectedGameVersion, StringComparison.Ordinal) ||
            !string.Equals(conversions.Status, ExpectedStatus, StringComparison.Ordinal) ||
            conversions.Entries is null ||
            conversions.Entries.Count != expectation.EntryCount ||
            conversions.Evidence is null ||
            conversions.Evidence.Count != expectation.EvidenceCount)
        {
            throw new InvalidDataException("Armor conversion Runtime fragment header or counts are invalid.");
        }

        var byParamId = new Dictionary<uint, ArmorConversionEntry>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var variantIds = new HashSet<string>(StringComparer.Ordinal);
        var previousParamId = 0u;
        foreach (var entry in conversions.Entries)
        {
            if (entry is null ||
                entry.ParamId == 0 ||
                entry.ParamId <= previousParamId ||
                !string.Equals(entry.ItemKey, $"armor:{entry.ParamId}", StringComparison.Ordinal) ||
                !string.Equals(entry.ArmorVariantId, $"armor-variant:{entry.ParamId}", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(entry.AlterationFamilyId) ||
                entry.TransformTargets is null ||
                !keys.Add(entry.ItemKey) ||
                !variantIds.Add(entry.ArmorVariantId) ||
                !byParamId.TryAdd(entry.ParamId, entry))
            {
                throw new InvalidDataException($"Invalid armor conversion entry: {entry?.ItemKey}");
            }

            previousParamId = entry.ParamId;
        }

        var incoming = conversions.Entries.ToDictionary(
            static entry => entry.ParamId,
            static _ => 0);
        var edges = new HashSet<(uint Source, uint Target)>();
        foreach (var entry in conversions.Entries)
        {
            var previousTarget = 0u;
            foreach (var targetId in entry.TransformTargets)
            {
                if (targetId == 0 ||
                    targetId <= previousTarget ||
                    targetId == entry.ParamId ||
                    !byParamId.TryGetValue(targetId, out var target) ||
                    !string.Equals(
                        entry.AlterationFamilyId,
                        target.AlterationFamilyId,
                        StringComparison.Ordinal) ||
                    !edges.Add((entry.ParamId, targetId)))
                {
                    throw new InvalidDataException(
                        $"Invalid armor conversion edge: {entry.ParamId} -> {targetId}");
                }

                incoming[targetId]++;
                previousTarget = targetId;
            }
        }

        if (edges.Count != expectation.DirectedEdgeCount ||
            edges.Count(edge => edges.Contains((edge.Target, edge.Source))) !=
                expectation.BidirectionalPairCount * 2)
        {
            throw new InvalidDataException("Armor conversion edge counts or directions are invalid.");
        }

        foreach (var entry in conversions.Entries)
        {
            var hasIncoming = incoming[entry.ParamId] > 0;
            var validAcquisition = entry.AcquisitionKind switch
            {
                ArmorAcquisitionKind.DirectOnly => entry.DirectlyObtainable && !hasIncoming,
                ArmorAcquisitionKind.TransformOnly => !entry.DirectlyObtainable && hasIncoming,
                ArmorAcquisitionKind.DirectOrTransform => entry.DirectlyObtainable && hasIncoming,
                ArmorAcquisitionKind.Unknown => false,
                _ => false,
            };
            if (!validAcquisition)
            {
                throw new InvalidDataException(
                    $"Armor acquisition classification is invalid: {entry.ItemKey}");
            }
        }

        ValidateClassificationCounts(conversions.Entries, expectation);
        ValidateEvidence(conversions.Evidence, edges);
        ValidateIndependentPairs(byParamId, expectation.IndependentPairs);
    }

    private static void ValidateClassificationCounts(
        IReadOnlyList<ArmorConversionEntry> entries,
        ArmorConversionDataPackExpectation expectation)
    {
        if (entries.Count(static entry => entry.VariantType is ArmorVariantType.Normal) !=
                expectation.NormalVariantCount ||
            entries.Count(static entry => entry.VariantType is ArmorVariantType.Altered) !=
                expectation.AlteredVariantCount ||
            entries.Count(static entry => entry.VariantType is ArmorVariantType.Special) !=
                expectation.SpecialVariantCount ||
            entries.Count(static entry => entry.AcquisitionKind is ArmorAcquisitionKind.DirectOnly) !=
                expectation.DirectOnlyCount ||
            entries.Count(static entry => entry.AcquisitionKind is ArmorAcquisitionKind.TransformOnly) !=
                expectation.TransformOnlyCount ||
            entries.Count(static entry => entry.AcquisitionKind is ArmorAcquisitionKind.DirectOrTransform) !=
                expectation.DirectOrTransformCount)
        {
            throw new InvalidDataException("Armor conversion classification counts are invalid.");
        }
    }

    private static void ValidateEvidence(
        IReadOnlyList<ArmorConversionEvidence> evidenceRows,
        IReadOnlySet<(uint Source, uint Target)> edges)
    {
        var evidenceEdges = new HashSet<(uint Source, uint Target)>();
        (uint Source, uint Target) previous = default;
        foreach (var evidence in evidenceRows)
        {
            var current = (Source: evidence.SourceParamId, Target: evidence.TargetParamId);
            if (current.Source == 0 ||
                current.Target == 0 ||
                (previous != default && CompareEdges(current, previous) <= 0) ||
                !evidenceEdges.Add(current) ||
                evidence.ShopRowIds is null ||
                evidence.ShopRowIds.Count != 2 ||
                evidence.ShopRowIds.Any(static id => id <= 0) ||
                !IsStrictlyIncreasing(evidence.ShopRowIds) ||
                evidence.MaterialSetIds is null ||
                evidence.MaterialSetIds.Count != 1 ||
                evidence.MaterialSetIds[0] <= 0)
            {
                throw new InvalidDataException(
                    $"Invalid armor conversion evidence: {current.Source} -> {current.Target}");
            }

            previous = current;
        }

        if (!evidenceEdges.SetEquals(edges))
        {
            throw new InvalidDataException("Armor conversion evidence does not match the directed edge set.");
        }
    }

    private static void ValidateIndependentPairs(
        IReadOnlyDictionary<uint, ArmorConversionEntry> entries,
        IReadOnlyList<ArmorIndependentPair> independentPairs)
    {
        foreach (var pair in independentPairs)
        {
            if (!entries.TryGetValue(pair.NormalParamId, out var normal) ||
                !entries.TryGetValue(pair.AlteredParamId, out var altered) ||
                normal.AcquisitionKind is not ArmorAcquisitionKind.DirectOnly ||
                altered.AcquisitionKind is not ArmorAcquisitionKind.DirectOnly ||
                normal.TransformTargets.Count != 0 ||
                altered.TransformTargets.Count != 0 ||
                normal.VariantType is not ArmorVariantType.Normal ||
                altered.VariantType is not ArmorVariantType.Altered)
            {
                throw new InvalidDataException(
                    $"Independent altered armor pair is invalid: {pair.NormalParamId}/{pair.AlteredParamId}");
            }
        }
    }

    private static int CompareEdges(
        (uint Source, uint Target) first,
        (uint Source, uint Target) second)
    {
        var source = first.Source.CompareTo(second.Source);
        return source != 0 ? source : first.Target.CompareTo(second.Target);
    }

    private static bool IsStrictlyIncreasing(IReadOnlyList<int> values)
    {
        for (var index = 1; index < values.Count; index++)
        {
            if (values[index] <= values[index - 1])
            {
                return false;
            }
        }

        return true;
    }

    private static string ResolveContainedPath(string root, string logicalPath)
    {
        if (string.IsNullOrWhiteSpace(logicalPath) ||
            Path.IsPathRooted(logicalPath) ||
            !string.Equals(Path.GetExtension(logicalPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Armor conversion Runtime path is unsafe: {logicalPath}");
        }

        var path = Path.GetFullPath(Path.Combine(root, logicalPath.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                Path.GetFileName(path),
                ArmorConversionDataPackLayout.ManifestFileName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Armor conversion Runtime path escaped the pack: {logicalPath}");
        }

        return path;
    }

    private static async Task<T> ReadJsonAsync<T>(
        string path,
        string source,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"{source} was not found.");
        }

        try
        {
            await using var input = OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(input, JsonOptions, cancellationToken) ??
                throw new InvalidDataException($"{source} is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{source} is invalid JSON.", exception);
        }
    }

    private static void ValidateSha256(string? value, string source)
    {
        if (value is null || value.Length != 64 || !value.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException($"Invalid SHA-256 in {source}.");
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
