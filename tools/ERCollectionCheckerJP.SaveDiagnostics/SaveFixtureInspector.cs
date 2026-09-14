using System.Buffers.Binary;
using System.Security.Cryptography;
using ERCollectionCheckerJP.SaveParser.Container;
using ERCollectionCheckerJP.SaveParser.Flags;
using ERCollectionCheckerJP.SaveParser.IO;
using ERCollectionCheckerJP.SaveParser.Items;

namespace ERCollectionCheckerJP.SaveDiagnostics;

public sealed record SaveEntryInspection(
    int Index,
    string Name,
    int PayloadLength,
    string PayloadSha256,
    uint? SlotVersion,
    bool? EmptySlot,
    int? GaItemCount,
    int? GaItemEndOffset,
    IReadOnlyDictionary<string, int>? GaItemKinds,
    int? EquipmentItemIdsOffset,
    int? EquipmentHandlesOffset,
    int? EquipmentOwnedRecordCount,
    int? EquipmentUnresolvedRecordCount,
    IReadOnlyDictionary<string, int>? EquipmentKinds,
    int? EquippedSpellsStartOffset,
    int? EquippedSpellCount,
    int? SelectedSpellSlotIndex,
    int? InventoryStartOffset,
    int? InventoryOwnedRecordCount,
    int? InventoryUnresolvedRecordCount,
    IReadOnlyDictionary<string, int>? InventoryKinds,
    int? StorageStartOffset,
    int? StorageOwnedRecordCount,
    int? StorageUnresolvedRecordCount,
    IReadOnlyDictionary<string, int>? StorageKinds,
    int? GesturesStartOffset,
    int? GestureCount,
    int? EventFlagsStartOffset,
    int? ProjectileCount,
    int? UnlockedRegionCount,
    bool Md5Valid);

public sealed record SaveFixtureInspection(
    int SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    string SourceFileName,
    long SourceLength,
    string SourceSha256,
    string BinderVersion,
    bool ReadOnlyHashVerified,
    IReadOnlyList<SaveEntryInspection> Entries);

public static class SaveFixtureInspector
{
    public static async Task<SaveFixtureInspection> InspectAsync(
        InspectSaveOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        PathSafety.EnsureOutputOutsideSourceDirectories(options.OutputPath, options.SavePath);
        var savePath = Path.GetFullPath(options.SavePath);
        var before = await ReadOnlyFileSnapshotLoader.LoadAsync(savePath, cancellationToken);
        var container = EldenRingSaveContainerReader.Read(before.Bytes);
        var entries = new List<SaveEntryInspection>(container.Entries.Count);
        foreach (var entry in container.Entries)
        {
            var payload = EldenRingSaveContainerReader.ExtractPayload(before.Bytes, entry);
            var isCharacterSlot = entry.Index < 10;
            var slotVersion = isCharacterSlot
                ? BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, sizeof(uint)))
                : (uint?)null;
            var gaItems = isCharacterSlot && slotVersion != 0
                ? GaItemSectionReader.ReadSlot(payload)
                : null;
            var inventory = gaItems is not null
                ? InventorySectionReader.ReadSlot(payload, gaItems)
                : null;
            var equipment = gaItems is not null && inventory is not null
                ? EquipmentSectionReader.ReadSlot(payload, gaItems, inventory)
                : null;
            var equippedSpells = inventory is not null
                ? EquippedSpellSectionReader.ReadSlot(payload, inventory)
                : null;
            var storage = gaItems is not null && inventory is not null
                ? StorageSectionReader.ReadSlot(payload, gaItems, inventory)
                : null;
            var gestures = storage is not null
                ? GestureSectionReader.ReadSlot(payload, storage)
                : null;
            var eventFlags = storage is not null
                ? EventFlagsSectionReader.ReadSlot(payload, storage)
                : null;

            entries.Add(new SaveEntryInspection(
                entry.Index,
                entry.Name,
                payload.Length,
                Convert.ToHexString(SHA256.HashData(payload)),
                slotVersion,
                isCharacterSlot ? slotVersion == 0 : null,
                gaItems?.Records.Count,
                gaItems?.EndOffset,
                CountKinds(gaItems?.Records.Select(static item => item.Kind.ToString())),
                equipment?.ItemIdsOffset,
                equipment?.HandlesOffset,
                equipment?.Records.Count,
                equipment?.Records.Count(static item => item.ParamId is null),
                CountKinds(equipment?.Records.Select(static item => item.Kind.ToString())),
                equippedSpells?.StartOffset,
                equippedSpells?.Records.Count,
                equippedSpells?.SelectedSlotIndex,
                inventory?.StartOffset,
                inventory?.Records.Count,
                inventory?.Records.Count(static item => item.ParamId is null),
                CountKinds(inventory?.Records.Select(static item => item.Kind.ToString())),
                storage?.StartOffset,
                storage?.Records.Count,
                storage?.Records.Count(static item => item.ParamId is null),
                CountKinds(storage?.Records.Select(static item => item.Kind.ToString())),
                gestures?.StartOffset,
                gestures?.Records.Count,
                eventFlags?.StartOffset,
                eventFlags?.ProjectileCount,
                eventFlags?.UnlockedRegionCount,
                true));
        }

        var after = await ReadOnlyFileSnapshotLoader.LoadAsync(savePath, cancellationToken);
        return new SaveFixtureInspection(
            4,
            DateTimeOffset.UtcNow,
            Path.GetFileName(savePath),
            before.Bytes.LongLength,
            before.Sha256,
            container.BinderVersion,
            string.Equals(before.Sha256, after.Sha256, StringComparison.Ordinal),
            entries);
    }

    private static IReadOnlyDictionary<string, int>? CountKinds(IEnumerable<string>? kinds) =>
        kinds?.GroupBy(static item => item)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count());
}
