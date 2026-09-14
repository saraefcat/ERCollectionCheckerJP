using System.Buffers.Binary;
using ERCollectionCheckerJP.Domain.Items;
using ERCollectionCheckerJP.ItemDatabase.DataPacks;
using ERCollectionCheckerJP.SaveParser.Container;
using ERCollectionCheckerJP.SaveParser.Flags;
using ERCollectionCheckerJP.SaveParser.IO;
using ERCollectionCheckerJP.SaveParser.Items;

namespace ERCollectionCheckerJP.SaveDiagnostics;

public sealed record ComparedSaveFile(
    string FileName,
    string Sha256,
    bool ReadOnlyHashVerified);

public sealed record CatalogInventoryItem(
    string Key,
    string Name,
    ItemKind Kind,
    uint ParamId,
    string DetectionMethod);

public sealed record SaveSlotInventoryComparison(
    int SlotIndex,
    uint BeforeVersion,
    uint AfterVersion,
    bool PayloadChanged,
    int BeforeInventoryRecordCount,
    int AfterInventoryRecordCount,
    IReadOnlyList<string> BeforeOwnedCatalogKeys,
    IReadOnlyList<string> AfterOwnedCatalogKeys,
    IReadOnlyList<string> NewlyOwnedCatalogKeys,
    IReadOnlyList<string> NoLongerOwnedCatalogKeys);

public sealed record SaveInventoryComparison(
    int SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    ComparedSaveFile Before,
    ComparedSaveFile After,
    string DatabaseVersion,
    int? LikelyTargetSlot,
    IReadOnlyList<CatalogInventoryItem> Catalog,
    IReadOnlyList<SaveSlotInventoryComparison> Slots);

public static class SaveInventoryComparer
{
    public static async Task<SaveInventoryComparison> CompareAsync(
        CompareSaveInventoriesOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        PathSafety.EnsureOutputOutsideSourceDirectories(
            options.OutputPath,
            options.BeforeSavePath,
            options.AfterSavePath);

        var pack = await TarnishedPackDataPackLoader.LoadAsync(
            options.DataPackDirectory,
            cancellationToken);
        var catalog = BuildCatalog(pack);
        var catalogKeys = catalog.Select(static item => item.Key).ToHashSet(StringComparer.Ordinal);
        var aliasLookup = pack.CanonicalAliases.Aliases.ToDictionary(
            static item => (item.SourceKind, checked((uint)item.ObservedParamId)),
            static item => item.CanonicalKey);
        var directLookup = pack.Items.Items
            .Where(static item => item.Kind is not ItemKind.TorrentAttire)
            .ToDictionary(
                static item => (item.Kind, item.ParamId),
                static item => item.CanonicalKey);
        var flagAddresses = pack.EventFlagAddresses.Flags.ToDictionary(
            static item => item.ItemKey,
            static item => new EventFlagAddress(
                item.FlagId,
                item.ByteIndex,
                item.BitIndex),
            StringComparer.Ordinal);

        var beforePath = Path.GetFullPath(options.BeforeSavePath);
        var afterPath = Path.GetFullPath(options.AfterSavePath);
        var beforeSnapshot = await ReadSaveAsync(beforePath, cancellationToken);
        var afterSnapshot = await ReadSaveAsync(afterPath, cancellationToken);
        var slots = new List<SaveSlotInventoryComparison>(10);
        for (var index = 0; index < 10; index++)
        {
            var beforePayload = EldenRingSaveContainerReader.ExtractPayload(
                beforeSnapshot.File.Bytes,
                beforeSnapshot.Container.Entries[index]);
            var afterPayload = EldenRingSaveContainerReader.ExtractPayload(
                afterSnapshot.File.Bytes,
                afterSnapshot.Container.Entries[index]);
            var beforeVersion = BinaryPrimitives.ReadUInt32LittleEndian(beforePayload);
            var afterVersion = BinaryPrimitives.ReadUInt32LittleEndian(afterPayload);
            var beforeInventory = ReadInventory(beforePayload, beforeVersion);
            var afterInventory = ReadInventory(afterPayload, afterVersion);
            var beforeStorage = ReadStorage(beforePayload, beforeVersion, beforeInventory);
            var afterStorage = ReadStorage(afterPayload, afterVersion, afterInventory);
            var beforeOwned = ResolveOwnedCatalogKeys(
                beforePayload,
                beforeInventory,
                beforeStorage,
                aliasLookup,
                directLookup,
                flagAddresses,
                catalogKeys);
            var afterOwned = ResolveOwnedCatalogKeys(
                afterPayload,
                afterInventory,
                afterStorage,
                aliasLookup,
                directLookup,
                flagAddresses,
                catalogKeys);
            slots.Add(new SaveSlotInventoryComparison(
                index,
                beforeVersion,
                afterVersion,
                !beforePayload.AsSpan().SequenceEqual(afterPayload),
                beforeInventory?.Records.Count ?? 0,
                afterInventory?.Records.Count ?? 0,
                beforeOwned,
                afterOwned,
                afterOwned.Except(beforeOwned, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                beforeOwned.Except(afterOwned, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()));
        }

        var beforeAfterRead = await ReadOnlyFileSnapshotLoader.LoadAsync(beforePath, cancellationToken);
        var afterAfterRead = await ReadOnlyFileSnapshotLoader.LoadAsync(afterPath, cancellationToken);
        return new SaveInventoryComparison(
            1,
            DateTimeOffset.UtcNow,
            new ComparedSaveFile(
                Path.GetFileName(beforePath),
                beforeSnapshot.File.Sha256,
                string.Equals(beforeSnapshot.File.Sha256, beforeAfterRead.Sha256, StringComparison.Ordinal)),
            new ComparedSaveFile(
                Path.GetFileName(afterPath),
                afterSnapshot.File.Sha256,
                string.Equals(afterSnapshot.File.Sha256, afterAfterRead.Sha256, StringComparison.Ordinal)),
            pack.Manifest.DatabaseVersion,
            FindLikelyTargetSlot(slots),
            catalog,
            slots);
    }

    private static InventorySection? ReadInventory(byte[] payload, uint version)
    {
        if (version == 0)
        {
            return null;
        }

        var gaItems = GaItemSectionReader.ReadSlot(payload);
        return InventorySectionReader.ReadSlot(payload, gaItems);
    }

    private static StorageSection? ReadStorage(
        byte[] payload,
        uint version,
        InventorySection? inventory)
    {
        if (version == 0 || inventory is null)
        {
            return null;
        }

        var gaItems = GaItemSectionReader.ReadSlot(payload);
        return StorageSectionReader.ReadSlot(payload, gaItems, inventory);
    }

    private static IReadOnlyList<string> ResolveCatalogKeys(
        IEnumerable<InventoryItemRecord> records,
        IReadOnlyDictionary<(ItemKind Kind, uint ParamId), string> aliases,
        IReadOnlyDictionary<(ItemKind Kind, uint ParamId), string> direct,
        IReadOnlySet<string> catalogKeys)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            if (record.ParamId is not uint paramId)
            {
                continue;
            }

            string? key = null;
            if (record.Kind is ItemKind.Weapon)
            {
                var affinityParamId = paramId - (paramId % 100);
                aliases.TryGetValue((record.Kind, affinityParamId), out key);
            }
            else
            {
                direct.TryGetValue((record.Kind, paramId), out key);
            }

            if (key is not null && catalogKeys.Contains(key))
            {
                result.Add(key);
            }
        }

        return result.Order(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> ResolveOwnedCatalogKeys(
        ReadOnlySpan<byte> payload,
        InventorySection? inventory,
        StorageSection? storage,
        IReadOnlyDictionary<(ItemKind Kind, uint ParamId), string> aliases,
        IReadOnlyDictionary<(ItemKind Kind, uint ParamId), string> direct,
        IReadOnlyDictionary<string, EventFlagAddress> flagAddresses,
        IReadOnlySet<string> catalogKeys)
    {
        if (inventory is null || storage is null)
        {
            return [];
        }

        var result = ResolveCatalogKeys(
                inventory.Records.Concat(storage.Records),
                aliases,
                direct,
                catalogKeys)
            .ToHashSet(StringComparer.Ordinal);
        var flags = EventFlagsSectionReader.ReadSlot(payload, storage);
        foreach (var (key, address) in flagAddresses)
        {
            if (catalogKeys.Contains(key) && EventFlagsSectionReader.IsSet(payload, flags, address))
            {
                result.Add(key);
            }
        }

        return result.Order(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<CatalogInventoryItem> BuildCatalog(TarnishedPackDataPack pack)
    {
        var names = pack.Japanese.Entries.ToDictionary(
            static item => item.Key,
            static item => item.Name,
            StringComparer.Ordinal);
        var methodLookup = pack.DetectionRules.Entries.ToDictionary(
            static item => item.Key,
            static item => item.Rules[0].Type.ToString(),
            StringComparer.Ordinal);
        return pack.Items.Items
            .Select(item => new CatalogInventoryItem(
                item.Key,
                names[item.Key],
                item.Kind,
                item.ParamId,
                methodLookup[item.Key]))
            .OrderBy(static item => item.Kind)
            .ThenBy(static item => item.ParamId)
            .ToArray();
    }

    private static int? FindLikelyTargetSlot(IReadOnlyList<SaveSlotInventoryComparison> slots)
    {
        var ranked = slots
            .Where(static slot => slot.AfterVersion != 0 && slot.NewlyOwnedCatalogKeys.Count > 0)
            .OrderByDescending(static slot => slot.AfterOwnedCatalogKeys.Count)
            .ThenByDescending(static slot => slot.NewlyOwnedCatalogKeys.Count)
            .ToArray();
        if (ranked.Length == 0)
        {
            return null;
        }

        if (ranked.Length > 1 &&
            ranked[0].AfterOwnedCatalogKeys.Count == ranked[1].AfterOwnedCatalogKeys.Count &&
            ranked[0].NewlyOwnedCatalogKeys.Count == ranked[1].NewlyOwnedCatalogKeys.Count)
        {
            return null;
        }

        return ranked[0].SlotIndex;
    }

    private static async Task<(ReadOnlyFileSnapshot File, EldenRingSaveContainer Container)> ReadSaveAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var file = await ReadOnlyFileSnapshotLoader.LoadAsync(path, cancellationToken);
        return (file, EldenRingSaveContainerReader.Read(file.Bytes));
    }
}
