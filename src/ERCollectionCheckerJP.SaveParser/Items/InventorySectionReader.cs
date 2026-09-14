using System.Buffers.Binary;
using ERCollectionCheckerJP.Domain.Items;

namespace ERCollectionCheckerJP.SaveParser.Items;

public enum InventoryContainer
{
    Common,
    Key,
}

public enum InventoryItemResolution
{
    GaItemMap,
    EncodedInHandle,
    Unresolved,
}

public sealed record InventoryItemRecord(
    InventoryContainer Container,
    int Index,
    int Offset,
    uint Handle,
    uint Quantity,
    uint AcquisitionIndex,
    ItemKind Kind,
    uint? RawItemId,
    uint? ParamId,
    InventoryItemResolution Resolution);

public sealed record InventorySection(
    int AnchorOffset,
    int StartOffset,
    int EndOffset,
    uint CommonItemCountHeader,
    uint KeyItemCountHeader,
    uint NextEquipIndex,
    uint NextAcquisitionSortId,
    IReadOnlyList<InventoryItemRecord> Records);

/// <summary>
/// Reads the fixed-capacity held inventory from a current ELDEN RING character
/// slot. The reader validates the PlayerGameData/SP-effect boundary before it
/// trusts the inventory offset and never mutates the supplied bytes.
/// </summary>
public static class InventorySectionReader
{
    public const int CommonItemCapacity = 0xA80;
    public const int KeyItemCapacity = 0x180;
    public const int RecordSize = 12;

    private const int PlayerGameDataSize = 0x1B0;
    private const int InventoryStartFromAnchor = 505;
    private const int KeyCountHeaderSize = sizeof(uint);

    // The anchor begins with the final byte before the SP-effect section,
    // followed by four repeated empty-effect sentinels.
    private static ReadOnlySpan<byte> AnchorPattern =>
    [
        0x00,
        0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    ];

    public static InventorySection ReadSlot(
        ReadOnlySpan<byte> slot,
        GaItemSection gaItems)
    {
        ArgumentNullException.ThrowIfNull(gaItems);
        var anchorOffset = checked(gaItems.EndOffset + PlayerGameDataSize - 1);
        EnsureAvailable(slot, anchorOffset, AnchorPattern.Length, "inventory anchor");
        if (!slot.Slice(anchorOffset, AnchorPattern.Length).SequenceEqual(AnchorPattern))
        {
            throw new InvalidDataException(
                "The PlayerGameData boundary marker is missing at the expected offset.");
        }

        var startOffset = checked(anchorOffset + InventoryStartFromAnchor);
        var commonHeaderOffset = checked(startOffset - sizeof(uint));
        var commonBytes = checked(CommonItemCapacity * RecordSize);
        var keyHeaderOffset = checked(startOffset + commonBytes);
        var keyStartOffset = checked(keyHeaderOffset + KeyCountHeaderSize);
        var keyBytes = checked(KeyItemCapacity * RecordSize);
        var countersOffset = checked(keyStartOffset + keyBytes);
        var endOffset = checked(countersOffset + (sizeof(uint) * 2));
        EnsureAvailable(slot, commonHeaderOffset, endOffset - commonHeaderOffset, "inventory");

        var records = new List<InventoryItemRecord>(CommonItemCapacity + KeyItemCapacity);
        ReadRecords(
            slot,
            gaItems,
            InventoryContainer.Common,
            startOffset,
            CommonItemCapacity,
            records);
        ReadRecords(
            slot,
            gaItems,
            InventoryContainer.Key,
            keyStartOffset,
            KeyItemCapacity,
            records);

        return new InventorySection(
            anchorOffset,
            startOffset,
            endOffset,
            BinaryPrimitives.ReadUInt32LittleEndian(slot.Slice(commonHeaderOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(slot.Slice(keyHeaderOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(slot.Slice(countersOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(slot.Slice(countersOffset + sizeof(uint), sizeof(uint))),
            records);
    }

    private static void ReadRecords(
        ReadOnlySpan<byte> slot,
        GaItemSection gaItems,
        InventoryContainer container,
        int startOffset,
        int count,
        ICollection<InventoryItemRecord> records)
    {
        for (var index = 0; index < count; index++)
        {
            var offset = checked(startOffset + (index * RecordSize));
            var handle = BinaryPrimitives.ReadUInt32LittleEndian(slot.Slice(offset, sizeof(uint)));
            if (handle is 0 or uint.MaxValue)
            {
                continue;
            }

            var quantity = BinaryPrimitives.ReadUInt32LittleEndian(
                slot.Slice(offset + sizeof(uint), sizeof(uint)));
            var acquisitionIndex = BinaryPrimitives.ReadUInt32LittleEndian(
                slot.Slice(offset + (sizeof(uint) * 2), sizeof(uint)));
            var kind = DecodeHandleKind(handle);
            var (rawItemId, resolution) = ResolveRawItemId(handle, kind, gaItems);
            if (rawItemId is uint resolved && GaItemSectionReader.DecodeKind(resolved) != kind)
            {
                throw new InvalidDataException(
                    $"Inventory {container} item {index} resolves to a different item kind.");
            }

            records.Add(new InventoryItemRecord(
                container,
                index,
                offset,
                handle,
                quantity & 0x7FFF_FFFF,
                acquisitionIndex,
                kind,
                rawItemId,
                rawItemId & 0x0FFF_FFFF,
                resolution));
        }
    }

    public static ItemKind DecodeHandleKind(uint handle) => (handle & 0xF000_0000) switch
    {
        0x8000_0000 => ItemKind.Weapon,
        0x9000_0000 => ItemKind.Armor,
        0xA000_0000 => ItemKind.Accessory,
        0xB000_0000 => ItemKind.Goods,
        0xC000_0000 => ItemKind.AshOfWar,
        _ => throw new InvalidDataException($"Unsupported inventory handle prefix: 0x{handle:X8}."),
    };

    public static (uint? RawItemId, InventoryItemResolution Resolution) ResolveRawItemId(
        uint handle,
        ItemKind kind,
        GaItemSection gaItems)
    {
        if (gaItems.HandleToRawItemId.TryGetValue(handle, out var mapped))
        {
            return (mapped, InventoryItemResolution.GaItemMap);
        }

        // Talismans and goods use a direct handle whose low 28 bits are the
        // PARAM ID. Instance-backed weapons, armor and Ashes require GaMap.
        return kind switch
        {
            ItemKind.Accessory =>
                ((handle & 0x0FFF_FFFF) | 0x2000_0000, InventoryItemResolution.EncodedInHandle),
            ItemKind.Goods =>
                ((handle & 0x0FFF_FFFF) | 0x4000_0000, InventoryItemResolution.EncodedInHandle),
            _ => (null, InventoryItemResolution.Unresolved),
        };
    }

    private static void EnsureAvailable(
        ReadOnlySpan<byte> bytes,
        int offset,
        int length,
        string section)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length - length)
        {
            throw new InvalidDataException($"The {section} range exceeds the save slot.");
        }
    }
}
