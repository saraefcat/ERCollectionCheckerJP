using System.Buffers.Binary;

namespace ERCollectionCheckerJP.SaveParser.Items;

public sealed record StorageSection(
    int HeaderOffset,
    int StartOffset,
    int EndOffset,
    int ProjectileCount,
    uint CommonItemCountHeader,
    uint KeyItemCountHeader,
    uint NextEquipIndex,
    uint NextAcquisitionSortId,
    IReadOnlyList<InventoryItemRecord> Records);

/// <summary>Reads the fixed-capacity storage box without modifying the slot.</summary>
public static class StorageSectionReader
{
    public const int CommonItemCapacity = 0x780;
    public const int KeyItemCapacity = 0x80;
    public const int RecordSize = InventorySectionReader.RecordSize;
    private const int MaximumProjectileCount = 4096;

    public static StorageSection ReadSlot(
        ReadOnlySpan<byte> slot,
        GaItemSection gaItems,
        InventorySection inventory)
    {
        ArgumentNullException.ThrowIfNull(gaItems);
        ArgumentNullException.ThrowIfNull(inventory);

        var projectileCountOffset = checked(
            inventory.EndOffset + 0x74 + 0x8C + 0x18);
        EnsureAvailable(slot, projectileCountOffset, sizeof(uint), "acquired projectile count");
        var projectileCountValue = BinaryPrimitives.ReadUInt32LittleEndian(
            slot.Slice(projectileCountOffset, sizeof(uint)));
        if (projectileCountValue > MaximumProjectileCount)
        {
            throw new InvalidDataException("The acquired projectile count is outside the supported range.");
        }

        var projectileCount = checked((int)projectileCountValue);
        var afterProjectiles = checked(
            projectileCountOffset + sizeof(uint) + (projectileCount * 8));
        var headerOffset = checked(afterProjectiles + 0x9C + 0x0C + 0x12F);
        var startOffset = checked(headerOffset + sizeof(uint));
        var commonBytes = checked(CommonItemCapacity * RecordSize);
        var keyHeaderOffset = checked(startOffset + commonBytes);
        var keyStartOffset = checked(keyHeaderOffset + sizeof(uint));
        var keyBytes = checked(KeyItemCapacity * RecordSize);
        var countersOffset = checked(keyStartOffset + keyBytes);
        var endOffset = checked(countersOffset + (sizeof(uint) * 2));
        EnsureAvailable(slot, headerOffset, endOffset - headerOffset, "storage box");

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

        return new StorageSection(
            headerOffset,
            startOffset,
            endOffset,
            projectileCount,
            BinaryPrimitives.ReadUInt32LittleEndian(slot.Slice(headerOffset, sizeof(uint))),
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

            var kind = InventorySectionReader.DecodeHandleKind(handle);
            var (rawItemId, resolution) = InventorySectionReader.ResolveRawItemId(
                handle,
                kind,
                gaItems);
            if (rawItemId is uint resolved && GaItemSectionReader.DecodeKind(resolved) != kind)
            {
                throw new InvalidDataException(
                    $"Storage {container} item {index} resolves to a different item kind.");
            }

            records.Add(new InventoryItemRecord(
                container,
                index,
                offset,
                handle,
                BinaryPrimitives.ReadUInt32LittleEndian(slot.Slice(offset + 4, 4)) & 0x7FFF_FFFF,
                BinaryPrimitives.ReadUInt32LittleEndian(slot.Slice(offset + 8, 4)),
                kind,
                rawItemId,
                rawItemId & 0x0FFF_FFFF,
                resolution));
        }
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
