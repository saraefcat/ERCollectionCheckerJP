using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using ERCollectionCheckerJP.Domain.Items;

namespace ERCollectionCheckerJP.ItemDatabase.DataPacks;

public static class TarnishedPackDataPackLayout
{
    public const string ManifestFileName = "manifest.json";
    public const string ItemsFileName = "items.tarnished-pack.json";
    public const string JapaneseLocalizationFileName = "localization.tarnished-pack.ja.json";
    public const string EnglishLocalizationFileName = "localization.tarnished-pack.en.json";
    public const string DetectionRulesFileName = "detection-rules.tarnished-pack.json";
    public const string CanonicalAliasesFileName = "canonical-aliases.tarnished-pack.json";
    public const string EventFlagAddressesFileName = "event-flag-addresses.tarnished-pack.json";

    public static readonly IReadOnlyList<string> PayloadFileNames =
    [
        CanonicalAliasesFileName,
        DetectionRulesFileName,
        EventFlagAddressesFileName,
        ItemsFileName,
        EnglishLocalizationFileName,
        JapaneseLocalizationFileName,
    ];
}

public sealed record RuntimeDataPackFile(
    string Path,
    long ByteLength,
    string Sha256);

public sealed record RuntimeDataPackManifest(
    int SchemaVersion,
    string Game,
    string AppVersion,
    string RegulationVersion,
    string DatabaseVersion,
    string Status,
    string SourcePackManifestSha256,
    IReadOnlyList<string> Languages,
    IReadOnlyList<RuntimeDataPackFile> Files);

public sealed record RuntimeItemDefinition(
    string Key,
    uint ParamId,
    ItemKind Kind,
    string Category,
    string? SubCategory,
    string CanonicalKey,
    string? CollectionGroupId,
    ContentPack ContentPack,
    DataPresence DataPresence,
    Obtainability Obtainability,
    ExclusionReason? ExclusionReason,
    bool IncludeInCollectionMode,
    bool IncludeInStrictMode,
    IReadOnlyList<DetectionRule> DetectionRules,
    bool IsDataOnly)
{
    public ItemDefinition ToDomain() => new()
    {
        Key = Key,
        ParamId = ParamId,
        Kind = Kind,
        Category = Category,
        SubCategory = SubCategory,
        CanonicalKey = CanonicalKey,
        CollectionGroupId = CollectionGroupId,
        ContentPack = ContentPack,
        DataPresence = DataPresence,
        Obtainability = Obtainability,
        ExclusionReason = ExclusionReason,
        IncludeInCollectionMode = IncludeInCollectionMode,
        IncludeInStrictMode = IncludeInStrictMode,
        DetectionRules = DetectionRules,
    };
}

public sealed record ItemDefinitionFragment(
    int SchemaVersion,
    string GameVersion,
    string Status,
    IReadOnlyList<RuntimeItemDefinition> Items);

public sealed record LocalizationFragmentEntry(string Key, string Name);

public sealed record LocalizationFragment(
    int SchemaVersion,
    string GameVersion,
    string Language,
    IReadOnlyList<LocalizationFragmentEntry> Entries);

public sealed record DetectionRuleFragmentEntry(
    string Key,
    IReadOnlyList<DetectionRule> Rules);

public sealed record DetectionRuleFragment(
    int SchemaVersion,
    string GameVersion,
    string Status,
    IReadOnlyList<DetectionRuleFragmentEntry> Entries);

public sealed record CanonicalAlias(
    ItemKind SourceKind,
    int ObservedParamId,
    string CanonicalKey);

public sealed record CanonicalAliasFragment(
    int SchemaVersion,
    string GameVersion,
    IReadOnlyList<CanonicalAlias> Aliases);

public sealed record EventFlagAddressFragmentEntry(
    string ItemKey,
    uint FlagId,
    int ByteIndex,
    byte BitIndex);

public sealed record EventFlagAddressFragment(
    int SchemaVersion,
    string GameVersion,
    string Status,
    IReadOnlyList<EventFlagAddressFragmentEntry> Flags);

public sealed record TarnishedPackDataPack(
    RuntimeDataPackManifest Manifest,
    ItemDefinitionFragment Items,
    LocalizationFragment Japanese,
    LocalizationFragment English,
    DetectionRuleFragment DetectionRules,
    CanonicalAliasFragment CanonicalAliases,
    EventFlagAddressFragment EventFlagAddresses)
{
    public IReadOnlyList<ItemDefinition> DomainItems =>
        Items.Items.Select(static item => item.ToDomain()).ToArray();
}

/// <summary>
/// Loads the Checker-owned Tarnished Pack fragment. Every payload is accepted
/// only after the manifest, containment, byte length, SHA-256 and cross-file
/// invariants have been validated.
/// </summary>
public static class TarnishedPackDataPackLoader
{
    private const int ExpectedSchemaVersion = 1;
    private const string ExpectedGame = "EldenRing";
    private const string ExpectedGameVersion = "1.17";
    private const string ExpectedStatus = "reviewed-fragment";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    public static async Task<TarnishedPackDataPack> LoadAsync(
        string packDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packDirectory);
        var root = Path.GetFullPath(packDirectory);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Runtime data pack was not found: {root}");
        }

        var manifestPath = Path.Combine(root, TarnishedPackDataPackLayout.ManifestFileName);
        var manifest = await ReadJsonAsync<RuntimeDataPackManifest>(
            manifestPath,
            "Runtime data pack manifest",
            cancellationToken);
        ValidateManifest(manifest);
        await ValidatePayloadFilesAsync(root, manifest, cancellationToken);

        var items = await ReadPayloadAsync<ItemDefinitionFragment>(
            root,
            TarnishedPackDataPackLayout.ItemsFileName,
            cancellationToken);
        var ja = await ReadPayloadAsync<LocalizationFragment>(
            root,
            TarnishedPackDataPackLayout.JapaneseLocalizationFileName,
            cancellationToken);
        var en = await ReadPayloadAsync<LocalizationFragment>(
            root,
            TarnishedPackDataPackLayout.EnglishLocalizationFileName,
            cancellationToken);
        var rules = await ReadPayloadAsync<DetectionRuleFragment>(
            root,
            TarnishedPackDataPackLayout.DetectionRulesFileName,
            cancellationToken);
        var aliases = await ReadPayloadAsync<CanonicalAliasFragment>(
            root,
            TarnishedPackDataPackLayout.CanonicalAliasesFileName,
            cancellationToken);
        var flags = await ReadPayloadAsync<EventFlagAddressFragment>(
            root,
            TarnishedPackDataPackLayout.EventFlagAddressesFileName,
            cancellationToken);

        ValidateContent(items, ja, en, rules, aliases, flags);
        return new TarnishedPackDataPack(manifest, items, ja, en, rules, aliases, flags);
    }

    private static void ValidateManifest(RuntimeDataPackManifest manifest)
    {
        if (manifest.SchemaVersion != ExpectedSchemaVersion ||
            !string.Equals(manifest.Game, ExpectedGame, StringComparison.Ordinal) ||
            !string.Equals(manifest.AppVersion, ExpectedGameVersion, StringComparison.Ordinal) ||
            !string.Equals(manifest.RegulationVersion, ExpectedGameVersion, StringComparison.Ordinal) ||
            !string.Equals(manifest.Status, ExpectedStatus, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(manifest.DatabaseVersion))
        {
            throw new InvalidDataException("Runtime data pack manifest version or status is unsupported.");
        }

        ValidateSha256(manifest.SourcePackManifestSha256, "source pack manifest");
        var languages = manifest.Languages?.ToHashSet(StringComparer.Ordinal) ?? [];
        if (manifest.Languages is null ||
            manifest.Languages.Count != 2 ||
            !languages.SetEquals(["ja", "en"]))
        {
            throw new InvalidDataException("Runtime data pack must declare exactly ja and en.");
        }

        if (manifest.Files is null || manifest.Files.Count == 0)
        {
            throw new InvalidDataException("Runtime data pack manifest has no payloads.");
        }

        var expected = TarnishedPackDataPackLayout.PayloadFileNames.ToHashSet(StringComparer.Ordinal);
        var actual = manifest.Files.Select(static file => file.Path).ToArray();
        if (!actual.SequenceEqual(actual.Order(StringComparer.Ordinal), StringComparer.Ordinal) ||
            actual.Distinct(StringComparer.OrdinalIgnoreCase).Count() != actual.Length ||
            !expected.SetEquals(actual))
        {
            throw new InvalidDataException("Runtime data pack payload list is incomplete, duplicated, or unsorted.");
        }

        foreach (var file in manifest.Files)
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
        RuntimeDataPackManifest manifest,
        CancellationToken cancellationToken)
    {
        var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files)
        {
            var path = ResolveContainedPath(root, file.Path);
            listed.Add(file.Path.Replace('\\', '/'));
            if (!File.Exists(path))
            {
                throw new InvalidDataException($"Runtime payload is missing: {file.Path}");
            }

            var info = new FileInfo(path);
            if (info.Length != file.ByteLength)
            {
                throw new InvalidDataException($"Runtime payload length mismatch: {file.Path}");
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
                TarnishedPackDataPackLayout.ManifestFileName,
                StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!listed.SetEquals(onDisk))
        {
            throw new InvalidDataException("Runtime data pack contains an unlisted or missing JSON payload.");
        }
    }

    private static async Task<T> ReadPayloadAsync<T>(
        string root,
        string logicalPath,
        CancellationToken cancellationToken)
    {
        var path = ResolveContainedPath(root, logicalPath);
        var result = await ReadJsonAsync<T>(path, logicalPath, cancellationToken);
        await using var input = OpenRead(path);
        using var document = await JsonDocument.ParseAsync(input, cancellationToken: cancellationToken);
        if (document.RootElement.ValueKind is not JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("schemaVersion", out var schema) ||
            !schema.TryGetInt32(out var value) ||
            value != ExpectedSchemaVersion)
        {
            throw new InvalidDataException($"Runtime payload schemaVersion is unsupported: {logicalPath}");
        }

        return result;
    }

    private static void ValidateContent(
        ItemDefinitionFragment items,
        LocalizationFragment ja,
        LocalizationFragment en,
        DetectionRuleFragment rules,
        CanonicalAliasFragment aliases,
        EventFlagAddressFragment flags)
    {
        ValidateFragmentHeader(items.SchemaVersion, items.GameVersion, items.Status, ExpectedStatus, "items");
        ValidateFragmentHeader(rules.SchemaVersion, rules.GameVersion, rules.Status, "partial", "rules");
        ValidateFragmentHeader(flags.SchemaVersion, flags.GameVersion, flags.Status, ExpectedStatus, "flags");
        ValidateLocalizationHeader(ja, "ja");
        ValidateLocalizationHeader(en, "en");
        ValidateSchemaAndVersion(aliases.SchemaVersion, aliases.GameVersion, "aliases");

        if (items.Items is null || items.Items.Count != 29)
        {
            throw new InvalidDataException("Tarnished Pack fragment must contain exactly 29 items.");
        }

        var itemKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items.Items)
        {
            var expectedCategory = item.Kind switch
            {
                ItemKind.Weapon => "Weapons",
                ItemKind.Armor => "Armor",
                ItemKind.TorrentAttire => "TorrentAttire",
                _ => throw new InvalidDataException($"Unsupported Tarnished Pack item kind: {item.Kind}"),
            };
            if (string.IsNullOrWhiteSpace(item.Key) ||
                item.ParamId == 0 ||
                !itemKeys.Add(item.Key) ||
                !string.Equals(item.Category, expectedCategory, StringComparison.Ordinal) ||
                item.SubCategory is not null ||
                !string.Equals(item.CanonicalKey, item.Key, StringComparison.Ordinal) ||
                item.CollectionGroupId is not null ||
                item.ContentPack is not ContentPack.TarnishedPack ||
                item.DataPresence is not DataPresence.Present ||
                item.Obtainability is not Obtainability.Obtainable ||
                item.ExclusionReason is not null ||
                !item.IncludeInCollectionMode ||
                !item.IncludeInStrictMode ||
                item.IsDataOnly ||
                item.DetectionRules is null ||
                item.DetectionRules.Count != 0)
            {
                throw new InvalidDataException($"Invalid Tarnished Pack item definition: {item.Key}");
            }
        }

        if (items.Items.Count(static item => item.Kind is ItemKind.Weapon) != 8 ||
            items.Items.Count(static item => item.Kind is ItemKind.Armor) != 18 ||
            items.Items.Count(static item => item.Kind is ItemKind.TorrentAttire) != 3)
        {
            throw new InvalidDataException("Tarnished Pack category counts are invalid.");
        }

        ValidateLocalizationEntries(ja, itemKeys);
        ValidateLocalizationEntries(en, itemKeys);
        if (rules.Entries is null ||
            rules.Entries.Count != itemKeys.Count ||
            !itemKeys.SetEquals(rules.Entries.Select(static entry => entry.Key)) ||
            rules.Entries.Select(static entry => entry.Key).Distinct(StringComparer.Ordinal).Count() != itemKeys.Count)
        {
            throw new InvalidDataException("Detection rule keys do not match item keys.");
        }

        ValidateRules(rules, items.Items);
        ValidateAliases(aliases, items.Items);
        ValidateFlags(flags, items.Items, rules);
    }

    private static void ValidateRules(
        DetectionRuleFragment fragment,
        IReadOnlyList<RuntimeItemDefinition> items)
    {
        var itemLookup = items.ToDictionary(static item => item.Key, StringComparer.Ordinal);
        foreach (var entry in fragment.Entries)
        {
            if (entry.Rules is null || entry.Rules.Count != 1)
            {
                throw new InvalidDataException($"Item must have exactly one top-level rule: {entry.Key}");
            }

            var rule = entry.Rules[0];
            if (rule is null || rule.Rules is null)
            {
                throw new InvalidDataException($"Detection rule contains null data: {entry.Key}");
            }

            var item = itemLookup[entry.Key];
            if (item.Kind is ItemKind.TorrentAttire)
            {
                if (rule.Type is not DetectionRuleType.TorrentAttireUnlock ||
                    rule.FlagId is null ||
                    rule.ParamId is not null ||
                    rule.Rules.Count != 0)
                {
                    throw new InvalidDataException($"Invalid Torrent attire rule: {entry.Key}");
                }

                continue;
            }

            if (rule.Type is not DetectionRuleType.AnyOf ||
                rule.ParamId is not null ||
                rule.FlagId is not null ||
                rule.Rules.Count != 2 ||
                rule.Rules[0] is not { } containerRule ||
                rule.Rules[1] is not { } equippedRule ||
                containerRule.Type is not DetectionRuleType.AnyContainerItem ||
                equippedRule.Type is not DetectionRuleType.EquippedItem ||
                containerRule.ParamId != item.ParamId ||
                equippedRule.ParamId != item.ParamId ||
                containerRule.FlagId is not null ||
                equippedRule.FlagId is not null ||
                containerRule.Rules is not { Count: 0 } ||
                equippedRule.Rules is not { Count: 0 })
            {
                throw new InvalidDataException($"Invalid physical item rule: {entry.Key}");
            }
        }
    }

    private static void ValidateAliases(
        CanonicalAliasFragment fragment,
        IReadOnlyList<RuntimeItemDefinition> items)
    {
        if (fragment.Aliases is null || fragment.Aliases.Count != 80)
        {
            throw new InvalidDataException("Tarnished Pack canonical aliases must contain exactly 80 rows.");
        }

        var itemKinds = items.ToDictionary(static item => item.Key, static item => item.Kind, StringComparer.Ordinal);
        var observed = new HashSet<int>();
        foreach (var alias in fragment.Aliases)
        {
            if (alias.SourceKind is not ItemKind.Weapon ||
                alias.ObservedParamId <= 0 ||
                !observed.Add(alias.ObservedParamId) ||
                !itemKinds.TryGetValue(alias.CanonicalKey, out var canonicalKind) ||
                canonicalKind is not ItemKind.Weapon)
            {
                throw new InvalidDataException("Canonical alias data is invalid.");
            }
        }
    }

    private static void ValidateFlags(
        EventFlagAddressFragment fragment,
        IReadOnlyList<RuntimeItemDefinition> items,
        DetectionRuleFragment rules)
    {
        if (fragment.Flags is null || fragment.Flags.Count != 3)
        {
            throw new InvalidDataException("Tarnished Pack event flag fragment must contain exactly three rows.");
        }

        var attireKeys = items
            .Where(static item => item.Kind is ItemKind.TorrentAttire)
            .Select(static item => item.Key)
            .ToHashSet(StringComparer.Ordinal);
        var ruleFlags = rules.Entries
            .Where(entry => attireKeys.Contains(entry.Key))
            .ToDictionary(static entry => entry.Key, static entry => entry.Rules[0].FlagId, StringComparer.Ordinal);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var flagIds = new HashSet<uint>();
        foreach (var flag in fragment.Flags)
        {
            if (!attireKeys.Contains(flag.ItemKey) ||
                !keys.Add(flag.ItemKey) ||
                flag.FlagId == 0 ||
                !flagIds.Add(flag.FlagId) ||
                flag.ByteIndex < 0 ||
                flag.BitIndex > 7 ||
                !ruleFlags.TryGetValue(flag.ItemKey, out var ruleFlag) ||
                ruleFlag != flag.FlagId)
            {
                throw new InvalidDataException("Tarnished Pack event flag data is invalid.");
            }
        }
    }

    private static void ValidateLocalizationEntries(
        LocalizationFragment fragment,
        IReadOnlySet<string> itemKeys)
    {
        if (fragment.Entries is null ||
            fragment.Entries.Count != itemKeys.Count ||
            fragment.Entries.Any(static entry => string.IsNullOrWhiteSpace(entry.Name)) ||
            fragment.Entries.Select(static entry => entry.Key).Distinct(StringComparer.Ordinal).Count() != itemKeys.Count ||
            !itemKeys.SetEquals(fragment.Entries.Select(static entry => entry.Key)))
        {
            throw new InvalidDataException($"Localization entries are invalid for {fragment.Language}.");
        }
    }

    private static void ValidateLocalizationHeader(LocalizationFragment fragment, string language)
    {
        ValidateSchemaAndVersion(fragment.SchemaVersion, fragment.GameVersion, $"localization {language}");
        if (!string.Equals(fragment.Language, language, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Localization language is invalid: {fragment.Language}");
        }
    }

    private static void ValidateFragmentHeader(
        int schemaVersion,
        string gameVersion,
        string status,
        string expectedStatus,
        string source)
    {
        ValidateSchemaAndVersion(schemaVersion, gameVersion, source);
        if (!string.Equals(status, expectedStatus, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Fragment status is unsupported: {source}");
        }
    }

    private static void ValidateSchemaAndVersion(int schemaVersion, string gameVersion, string source)
    {
        if (schemaVersion != ExpectedSchemaVersion ||
            !string.Equals(gameVersion, ExpectedGameVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Fragment schema or game version is unsupported: {source}");
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
                TarnishedPackDataPackLayout.ManifestFileName,
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
