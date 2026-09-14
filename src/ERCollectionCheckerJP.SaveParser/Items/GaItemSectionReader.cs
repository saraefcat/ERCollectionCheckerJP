using System.Buffers.Binary;
using ERCollectionCheckerJP.Domain.Items;

namespace ERCollectionCheckerJP.SaveParser.Items;

public sealed record GaItemRecord(
    int Index,
    int Offset,
    uint Handle,
    uint RawItemId,
    ItemKind Kind,
    uint? ParamId,
    int? Unknown2,
    int? Unknown3,
    uint? AttachedAshOfWarHandle,
    byte? Unknown5);

public sealed record GaItemSection(
    int StartOffset,
    int EndOffset,
    IReadOnlyList<GaItemRecord> Records,
    IReadOnlyDictionary<uint, uint> HandleToRawItemId);

public static class GaItemSectionReader
{
    public const int StartOffset = 0x20;

    public static GaItemSection ReadSlot(ReadOnlySpan<byte> slot)
    {
        if (slot.Length < StartOffset || slot.Length < sizeof(uint))
        {
            throw new InvalidDataException("Save slot is shorter than the GaItem header.");
        }

        var version = BinaryPrimitives.ReadUInt32LittleEndian(slot[..4]);
        if (version == 0)
        {
            return new GaItemSection(StartOffset, StartOffset, [], new Dictionary<uint, uint>());
        }

        return Read(slot, version <= 81 ? 5_118 : 5_120, StartOffset);
    }

    public static GaItemSection Read(
        ReadOnlySpan<byte> bytes,
        int recordCount,
        int startOffset = StartOffset)
    {
        if (recordCount < 0 || startOffset < 0 || startOffset > bytes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(recordCount));
        }

        var records = new List<GaItemRecord>(recordCount);
        var handleMap = new Dictionary<uint, uint>();
        var offset = startOffset;
        for (var index = 0; index < recordCount; index++)
        {
            EnsureAvailable(bytes, offset, 8, index);
            var handle = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
            var rawItemId = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset + 4, 4));
            if (rawItemId == uint.MaxValue)
            {
                if (handle is not (0 or uint.MaxValue))
                {
                    throw new InvalidDataException($"Empty GaItem {index} has a non-empty handle.");
                }

                records.Add(new GaItemRecord(
                    index,
                    offset,
                    handle,
                    rawItemId,
                    ItemKind.Unknown,
                    null,
                    null,
                    null,
                    null,
                    null));
                offset += 8;
                continue;
            }

            var kind = DecodeKind(rawItemId);
            ValidateHandleKind(handle, kind, index);
            int? unknown2 = null;
            int? unknown3 = null;
            uint? attachedAshOfWarHandle = null;
            byte? unknown5 = null;
            var recordSize = kind switch
            {
                ItemKind.Weapon => 21,
                ItemKind.Armor => 16,
                _ => 8,
            };
            EnsureAvailable(bytes, offset, recordSize, index);
            if (recordSize >= 16)
            {
                unknown2 = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 8, 4));
                unknown3 = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 12, 4));
            }

            if (kind is ItemKind.Weapon)
            {
                attachedAshOfWarHandle = BinaryPrimitives.ReadUInt32LittleEndian(
                    bytes.Slice(offset + 16, 4));
                unknown5 = bytes[offset + 20];
            }

            if (!handleMap.TryAdd(handle, rawItemId))
            {
                throw new InvalidDataException($"GaItem handle 0x{handle:X8} is duplicated.");
            }

            records.Add(new GaItemRecord(
                index,
                offset,
                handle,
                rawItemId,
                kind,
                rawItemId & 0x0FFF_FFFF,
                unknown2,
                unknown3,
                attachedAshOfWarHandle,
                unknown5));
            offset += recordSize;
        }

        return new GaItemSection(startOffset, offset, records, handleMap);
    }

    public static ItemKind DecodeKind(uint rawItemId) => (rawItemId & 0xF000_0000) switch
    {
        0x0000_0000 => ItemKind.Weapon,
        0x1000_0000 => ItemKind.Armor,
        0x2000_0000 => ItemKind.Accessory,
        0x4000_0000 => ItemKind.Goods,
        0x8000_0000 => ItemKind.AshOfWar,
        _ => throw new InvalidDataException($"Unsupported GaItem ID prefix: 0x{rawItemId:X8}."),
    };

    private static void ValidateHandleKind(uint handle, ItemKind kind, int index)
    {
        var expected = kind switch
        {
            ItemKind.Weapon => 0x8000_0000u,
            ItemKind.Armor => 0x9000_0000u,
            ItemKind.Accessory => 0xA000_0000u,
            ItemKind.Goods => 0xB000_0000u,
            ItemKind.AshOfWar => 0xC000_0000u,
            _ => throw new InvalidDataException($"Unsupported GaItem kind: {kind}."),
        };
        if ((handle & 0xF000_0000) != expected)
        {
            throw new InvalidDataException(
                $"GaItem {index} handle type does not match its item ID type.");
        }
    }

    private static void EnsureAvailable(
        ReadOnlySpan<byte> bytes,
        int offset,
        int length,
        int index)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length - length)
        {
            throw new InvalidDataException($"GaItem {index} exceeds the save slot.");
        }
    }
}
