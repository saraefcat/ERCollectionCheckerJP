using System.Buffers.Binary;
using ERCollectionCheckerJP.Domain.Items;

namespace ERCollectionCheckerJP.SaveParser.Items;

public enum EquippedItemSlot
{
    LeftHandArmament1,
    RightHandArmament1,
    LeftHandArmament2,
    RightHandArmament2,
    LeftHandArmament3,
    RightHandArmament3,
    Arrow1,
    Bolt1,
    Arrow2,
    Bolt2,
    Unknown10,
    Unknown11,
    Head,
    Chest,
    Arms,
    Legs,
    Unknown16,
    Talisman1,
    Talisman2,
    Talisman3,
    Talisman4,
    Talisman5,
}

public sealed record EquippedItemRecord(
    EquippedItemSlot Slot,
    int Index,
    int ItemIdOffset,
    int HandleOffset,
    uint EncodedItemId,
    uint Handle,
    ItemKind Kind,
    uint? RawItemId,
    uint? ParamId,
    InventoryItemResolution Resolution);

public sealed record EquipmentSection(
    int ItemIdsOffset,
    int HandlesOffset,
    int EndOffset,
    IReadOnlyList<EquippedItemRecord> Records);

/// <summary>
/// Reads the known item-bearing slots from the two 22-entry equipment tables.
/// Unknown slots are deliberately not assigned collection semantics.
/// </summary>
public static class EquipmentSectionReader
{
    public const int SlotCount = 22;
    public const int TableSize = SlotCount * sizeof(uint);

    private const int PlayerGameDataSize = 0x1B0;
    private const int SpEffectsSize = 0xD0;
    private const int EquippedItemIndicesSize = 0x58;
    private const int ActiveWeaponSlotsSize = 0x1C;
    private const int InventoryCountHeaderSize = sizeof(uint);

    public static EquipmentSection ReadSlot(
        ReadOnlySpan<byte> slot,
        GaItemSection gaItems,
        InventorySection inventory)
    {
        ArgumentNullException.ThrowIfNull(gaItems);
        ArgumentNullException.ThrowIfNull(inventory);

        var itemIdsOffset = checked(
            gaItems.EndOffset +
            PlayerGameDataSize +
            SpEffectsSize +
            EquippedItemIndicesSize +
            ActiveWeaponSlotsSize);
        var handlesOffset = checked(itemIdsOffset + TableSize);
        var endOffset = checked(handlesOffset + TableSize);
        EnsureAvailable(slot, itemIdsOffset, TableSize * 2);
        if (endOffset != inventory.StartOffset - InventoryCountHeaderSize)
        {
            throw new InvalidDataException(
                "The equipment tables do not end at the held-inventory boundary.");
        }

        var records = new List<EquippedItemRecord>();
        for (var index = 0; index < SlotCount; index++)
        {
            var kind = GetKnownItemKind(index);
            if (kind is null)
            {
                continue;
            }

            var itemIdOffset = checked(itemIdsOffset + (index * sizeof(uint)));
            var handleOffset = checked(handlesOffset + (index * sizeof(uint)));
            var encodedItemId = BinaryPrimitives.ReadUInt32LittleEndian(
                slot.Slice(itemIdOffset, sizeof(uint)));
            var handle = BinaryPrimitives.ReadUInt32LittleEndian(
                slot.Slice(handleOffset, sizeof(uint)));
            var itemIdIsEmpty = encodedItemId == uint.MaxValue;
            var handleIsEmpty = handle is 0 or uint.MaxValue;
            if (itemIdIsEmpty && handleIsEmpty)
            {
                continue;
            }

            if (itemIdIsEmpty || handleIsEmpty)
            {
                throw new InvalidDataException(
                    $"Equipment slot {index} has inconsistent item ID and handle emptiness.");
            }

            var resolvedKind = InventorySectionReader.DecodeHandleKind(handle);
            if (resolvedKind != kind.Value)
            {
                throw new InvalidDataException(
                    $"Equipment slot {index} handle has an unexpected item kind.");
            }

            var (rawItemId, resolution) = InventorySectionReader.ResolveRawItemId(
                handle,
                kind.Value,
                gaItems);
            uint? paramId = null;
            if (rawItemId is uint resolved)
            {
                if (GaItemSectionReader.DecodeKind(resolved) != kind.Value)
                {
                    throw new InvalidDataException(
                        $"Equipment slot {index} resolves to a different item kind.");
                }

                paramId = resolved & 0x0FFF_FFFF;
                if (!MatchesEncodedItemId(encodedItemId, paramId.Value, kind.Value))
                {
                    throw new InvalidDataException(
                        $"Equipment slot {index} item ID does not match its resolved handle.");
                }
            }

            records.Add(new EquippedItemRecord(
                (EquippedItemSlot)index,
                index,
                itemIdOffset,
                handleOffset,
                encodedItemId,
                handle,
                kind.Value,
                rawItemId,
                paramId,
                resolution));
        }

        return new EquipmentSection(itemIdsOffset, handlesOffset, endOffset, records);
    }

    private static ItemKind? GetKnownItemKind(int index) => index switch
    {
        >= 0 and <= 9 => ItemKind.Weapon,
        >= 12 and <= 15 => ItemKind.Armor,
        >= 17 and <= 21 => ItemKind.Accessory,
        _ => null,
    };

    private static bool MatchesEncodedItemId(uint encodedItemId, uint paramId, ItemKind kind) =>
        kind switch
        {
            ItemKind.Weapon or ItemKind.Armor =>
                encodedItemId == paramId || encodedItemId == (paramId | 0x8000_0000),
            ItemKind.Accessory => encodedItemId == paramId,
            _ => false,
        };

    private static void EnsureAvailable(ReadOnlySpan<byte> bytes, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length - length)
        {
            throw new InvalidDataException("The equipment range exceeds the save slot.");
        }
    }
}
