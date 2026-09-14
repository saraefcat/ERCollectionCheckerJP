using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using ERCollectionCheckerJP.Domain.Items;

namespace ERCollectionCheckerJP.ItemDatabase.DataPacks;

public static class ReviewedItemDatabaseDataPackLayout
{
    public const string ManifestFileName = "manifest.json";
    public const string ItemsFileName = "items.reviewed.json";
    public const string AliasesFileName = "aliases.reviewed.json";

    public static readonly IReadOnlyList<string> PayloadFileNames =
    [
        AliasesFileName,
        ItemsFileName,
    ];
}

public sealed record ReviewedItemDatabaseRuntimeManifest(
    int SchemaVersion,
    string Game,
    string AppVersion,
    string RegulationVersion,
    string DatabaseVersion,
    string Status,
    string SourcePackManifestSha256,
    string SourceDatabaseSha256,
    string SourceCatalogSha256,
    IReadOnlyList<string> Languages,
    IReadOnlyList<RuntimeDataPackFile> Files);

public sealed record ReviewedRuntimeItem(
    string Key,
    ItemKind Kind,
    ItemKind SourceKind,
    uint ParamId,
    string? NameJa,
    string? NameEn,
    ContentPack ContentPack)
{
    public DataPresence DataPresence { get; init; } = DataPresence.Present;

    public Obtainability Obtainability { get; init; } = Obtainability.Obtainable;

    public ExclusionReason? ExclusionReason { get; init; }

    public ItemIdentity Identity => new(SourceKind, ParamId);

    [JsonIgnore]
    public bool IsDataOnly =>
        DataPresence is DataPresence.Present &&
        Obtainability is Obtainability.Unobtainable;
}

public sealed record ReviewedRuntimeItemsFragment(
    int SchemaVersion,
    string GameVersion,
    string Status,
    IReadOnlyList<ReviewedRuntimeItem> Items);

public sealed record ReviewedRuntimeAlias(
    ItemKind SourceKind,
    uint ObservedParamId,
    uint CanonicalParamId)
{
    public ItemIdentity ObservedIdentity => new(SourceKind, ObservedParamId);

    public ItemIdentity CanonicalIdentity => new(SourceKind, CanonicalParamId);
}

public sealed record ReviewedRuntimeAliasesFragment(
    int SchemaVersion,
    string GameVersion,
    string Status,
    IReadOnlyList<ReviewedRuntimeAlias> Aliases);

public sealed record ReviewedItemDatabaseDataPack(
    ReviewedItemDatabaseRuntimeManifest Manifest,
    ReviewedRuntimeItemsFragment Items,
    ReviewedRuntimeAliasesFragment Aliases)
{
    public ReviewedItemIdentityResolver CreateIdentityResolver() => new(Items.Items, Aliases.Aliases);
}

public sealed class ReviewedItemIdentityResolver : IItemIdentityResolver
{
    private readonly HashSet<ItemIdentity> canonicalItems;
    private readonly IReadOnlyDictionary<ItemIdentity, ItemIdentity> aliases;

    internal ReviewedItemIdentityResolver(
        IEnumerable<ReviewedRuntimeItem> items,
        IEnumerable<ReviewedRuntimeAlias> aliases)
    {
        canonicalItems = items.Select(static item => item.Identity).ToHashSet();
        this.aliases = aliases.ToDictionary(
            static alias => alias.ObservedIdentity,
            static alias => alias.CanonicalIdentity);
    }

    public bool TryResolve(ItemIdentity observed, out ItemIdentity canonical)
    {
        if (aliases.TryGetValue(observed, out canonical))
        {
            return true;
        }

        if (canonicalItems.Contains(observed))
        {
            canonical = observed;
            return true;
        }

        canonical = default;
        return false;
    }

    public bool TryResolveIdentity(ItemIdentity observed, out ItemIdentity canonical) =>
        TryResolve(observed, out canonical);
}

/// <summary>
/// Loads the Checker-owned, reduced Runtime representation of the reviewed database.
/// It does not read generation artifacts or original game files.
/// </summary>
public static class ReviewedItemDatabaseDataPackLoader
{
    public const int ExpectedItemCount = 3367;
    public const int ExpectedObtainableItemCount = 2768;
    public const int ExpectedDataOnlyItemCount = 599;
    public const int ExpectedAliasCount = 3834;

    private const int ExpectedSchemaVersion = 2;
    private const string ExpectedGame = "EldenRing";
    private const string ExpectedGameVersion = "1.17";
    private const string ExpectedStatus = "reviewed-items-with-data-only";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    public static async Task<ReviewedItemDatabaseDataPack> LoadAsync(
        string packDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packDirectory);
        var root = Path.GetFullPath(packDirectory);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Runtime data pack was not found: {root}");
        }

        var manifest = await ReadJsonAsync<ReviewedItemDatabaseRuntimeManifest>(
            Path.Combine(root, ReviewedItemDatabaseDataPackLayout.ManifestFileName),
            "Reviewed Runtime manifest",
            cancellationToken);
        ValidateManifest(manifest);
        await ValidatePayloadFilesAsync(root, manifest, cancellationToken);

        var items = await ReadJsonAsync<ReviewedRuntimeItemsFragment>(
            Path.Combine(root, ReviewedItemDatabaseDataPackLayout.ItemsFileName),
            ReviewedItemDatabaseDataPackLayout.ItemsFileName,
            cancellationToken);
        var aliases = await ReadJsonAsync<ReviewedRuntimeAliasesFragment>(
            Path.Combine(root, ReviewedItemDatabaseDataPackLayout.AliasesFileName),
            ReviewedItemDatabaseDataPackLayout.AliasesFileName,
            cancellationToken);
        ValidateContent(items, aliases);
        return new ReviewedItemDatabaseDataPack(manifest, items, aliases);
    }

    private static void ValidateManifest(ReviewedItemDatabaseRuntimeManifest manifest)
    {
        if (manifest.SchemaVersion != ExpectedSchemaVersion ||
            !string.Equals(manifest.Game, ExpectedGame, StringComparison.Ordinal) ||
            !string.Equals(manifest.AppVersion, ExpectedGameVersion, StringComparison.Ordinal) ||
            !string.Equals(manifest.RegulationVersion, ExpectedGameVersion, StringComparison.Ordinal) ||
            !string.Equals(manifest.Status, ExpectedStatus, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(manifest.DatabaseVersion))
        {
            throw new InvalidDataException("Reviewed Runtime manifest version or status is unsupported.");
        }

        ValidateSha256(manifest.SourcePackManifestSha256, "source manifest");
        ValidateSha256(manifest.SourceDatabaseSha256, "source database");
        ValidateSha256(manifest.SourceCatalogSha256, "source catalog");
        if (manifest.Languages is null || !manifest.Languages.SequenceEqual(["ja", "en"]))
        {
            throw new InvalidDataException("Reviewed Runtime pack must declare ja and en in order.");
        }

        var files = manifest.Files ?? [];
        var paths = files.Select(static file => file.Path).ToArray();
        if (!paths.SequenceEqual(ReviewedItemDatabaseDataPackLayout.PayloadFileNames, StringComparer.Ordinal) ||
            paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != paths.Length)
        {
            throw new InvalidDataException("Reviewed Runtime payload list is incomplete, duplicated, or unsorted.");
        }

        foreach (var file in files)
        {
            if (file.ByteLength < 0)
            {
                throw new InvalidDataException($"Runtime payload has an invalid length: {file.Path}");
            }

            ValidateSha256(file.Sha256, file.Path);
        }
    }

    private static async Task ValidatePayloadFilesAsync(
        string root,
        ReviewedItemDatabaseRuntimeManifest manifest,
        CancellationToken cancellationToken)
    {
        var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files)
        {
            var path = ResolveContainedPath(root, file.Path);
            listed.Add(file.Path.Replace('\\', '/'));
            if (!File.Exists(path) || new FileInfo(path).Length != file.ByteLength)
            {
                throw new InvalidDataException($"Runtime payload is missing or has a length mismatch: {file.Path}");
            }

            await using var input = OpenRead(path);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken));
            if (!string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Runtime payload SHA-256 mismatch: {file.Path}");
            }
        }

        var onDisk = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Where(path => !string.Equals(
                path,
                ReviewedItemDatabaseDataPackLayout.ManifestFileName,
                StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!listed.SetEquals(onDisk))
        {
            throw new InvalidDataException("Reviewed Runtime pack contains an unlisted or missing payload.");
        }
    }

    private static void ValidateContent(
        ReviewedRuntimeItemsFragment items,
        ReviewedRuntimeAliasesFragment aliases)
    {
        ValidateHeader(items.SchemaVersion, items.GameVersion, items.Status, "items");
        ValidateHeader(aliases.SchemaVersion, aliases.GameVersion, aliases.Status, "aliases");
        if (items.Items is null || items.Items.Count != ExpectedItemCount)
        {
            throw new InvalidDataException($"Reviewed Runtime pack must contain {ExpectedItemCount} items.");
        }

        if (aliases.Aliases is null || aliases.Aliases.Count != ExpectedAliasCount)
        {
            throw new InvalidDataException($"Reviewed Runtime pack must contain {ExpectedAliasCount} aliases.");
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        var identities = new HashSet<ItemIdentity>();
        var obtainableCount = 0;
        var dataOnlyCount = 0;
        foreach (var item in items.Items)
        {
            ValidateSourceKind(item.SourceKind);
            var validAvailability = item.IsDataOnly
                ? item.ExclusionReason is Domain.Items.ExclusionReason.Unobtainable
                : item.DataPresence is DataPresence.Present &&
                  item.Obtainability is Obtainability.Obtainable &&
                  item.ExclusionReason is null &&
                  item.ContentPack is not ContentPack.Unknown;
            var hasValidNames = item.IsDataOnly
                ? (item.NameJa is null && item.NameEn is null) ||
                  (!string.IsNullOrWhiteSpace(item.NameJa) && !string.IsNullOrWhiteSpace(item.NameEn))
                : !string.IsNullOrWhiteSpace(item.NameJa) && !string.IsNullOrWhiteSpace(item.NameEn);
            if (item.ParamId == 0 ||
                !hasValidNames ||
                !keys.Add(item.Key) ||
                !identities.Add(item.Identity) ||
                !string.Equals(
                    item.Key,
                    $"{item.Kind.ToString().ToLowerInvariant()}:{item.ParamId}",
                    StringComparison.Ordinal) ||
                !IsCompatibleKind(item.SourceKind, item.Kind) ||
                !validAvailability)
            {
                throw new InvalidDataException($"Invalid reviewed Runtime item: {item.Key}");
            }

            if (item.IsDataOnly)
            {
                dataOnlyCount++;
            }
            else
            {
                obtainableCount++;
            }
        }

        if (obtainableCount != ExpectedObtainableItemCount ||
            dataOnlyCount != ExpectedDataOnlyItemCount)
        {
            throw new InvalidDataException("Reviewed Runtime availability counts are invalid.");
        }

        var observed = new HashSet<ItemIdentity>();
        foreach (var alias in aliases.Aliases)
        {
            ValidateSourceKind(alias.SourceKind);
            if (alias.ObservedParamId == 0 ||
                alias.CanonicalParamId == 0 ||
                alias.ObservedIdentity == alias.CanonicalIdentity ||
                !observed.Add(alias.ObservedIdentity) ||
                identities.Contains(alias.ObservedIdentity) ||
                !identities.Contains(alias.CanonicalIdentity))
            {
                throw new InvalidDataException(
                    $"Invalid reviewed Runtime alias: {alias.ObservedIdentity}");
            }
        }

        if (aliases.Aliases.Any(alias => observed.Contains(alias.CanonicalIdentity)))
        {
            throw new InvalidDataException("Reviewed Runtime aliases contain a chain or cycle.");
        }
    }

    private static bool IsCompatibleKind(ItemKind sourceKind, ItemKind itemKind) => sourceKind switch
    {
        ItemKind.Weapon => itemKind is ItemKind.Weapon,
        ItemKind.Armor => itemKind is ItemKind.Armor,
        ItemKind.Accessory => itemKind is ItemKind.Accessory,
        ItemKind.AshOfWar => itemKind is ItemKind.AshOfWar,
        ItemKind.Goods => itemKind is ItemKind.Goods or ItemKind.Sorcery or ItemKind.Incantation or
            ItemKind.Gesture or ItemKind.TorrentAttire,
        _ => false,
    };

    private static void ValidateSourceKind(ItemKind sourceKind)
    {
        if (sourceKind is not (ItemKind.Weapon or ItemKind.Armor or ItemKind.Accessory or
            ItemKind.Goods or ItemKind.AshOfWar))
        {
            throw new InvalidDataException($"Unsupported source item kind: {sourceKind}");
        }
    }

    private static void ValidateHeader(int schemaVersion, string gameVersion, string status, string source)
    {
        if (schemaVersion != ExpectedSchemaVersion ||
            !string.Equals(gameVersion, ExpectedGameVersion, StringComparison.Ordinal) ||
            !string.Equals(status, ExpectedStatus, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Reviewed Runtime {source} header is unsupported.");
        }
    }

    private static string ResolveContainedPath(string root, string logicalPath)
    {
        if (string.IsNullOrWhiteSpace(logicalPath) ||
            Path.IsPathRooted(logicalPath) ||
            !string.Equals(Path.GetExtension(logicalPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Runtime payload path is unsafe: {logicalPath}");
        }

        var path = Path.GetFullPath(Path.Combine(root, logicalPath.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                Path.GetFileName(path),
                ReviewedItemDatabaseDataPackLayout.ManifestFileName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Runtime payload path escaped the pack: {logicalPath}");
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
