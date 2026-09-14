using System.Buffers.Binary;

namespace ERCollectionCheckerJP.SaveParser.Items;

public sealed record GestureRecord(
    int Index,
    int Offset,
    uint SaveGestureId);

public sealed record GestureSection(
    int StartOffset,
    int EndOffset,
    IReadOnlyList<GestureRecord> Records);

/// <summary>
/// Reads the fixed 64-entry persistent gesture list immediately following the storage box.
/// Values are canonical save-slot gesture IDs and require a versioned GestureParam mapping
/// before they can be compared with Goods-backed catalog identities.
/// </summary>
public static class GestureSectionReader
{
    public const int SlotCount = 64;
    public const int RecordSize = sizeof(uint);
    public const int SectionSize = SlotCount * RecordSize;
    public const uint EmptyGestureId = 0xFFFF_FFFE;

    public static GestureSection ReadSlot(
        ReadOnlySpan<byte> slot,
        StorageSection storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        var startOffset = storage.EndOffset;
        var endOffset = checked(startOffset + SectionSize);
        EnsureAvailable(slot, startOffset, SectionSize);

        var records = new List<GestureRecord>(SlotCount);
        var ids = new HashSet<uint>();
        for (var index = 0; index < SlotCount; index++)
        {
            var offset = checked(startOffset + (index * RecordSize));
            var saveGestureId = BinaryPrimitives.ReadUInt32LittleEndian(
                slot.Slice(offset, RecordSize));
            if (saveGestureId == EmptyGestureId)
            {
                continue;
            }

            if (saveGestureId == uint.MaxValue || !ids.Add(saveGestureId))
            {
                throw new InvalidDataException(
                    $"Persistent gesture slot {index} has invalid value 0x{saveGestureId:X8}.");
            }

            records.Add(new GestureRecord(index, offset, saveGestureId));
        }

        return new GestureSection(startOffset, endOffset, records);
    }

    private static void EnsureAvailable(ReadOnlySpan<byte> bytes, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length - length)
        {
            throw new InvalidDataException("The persistent gesture range exceeds the save slot.");
        }
    }
}
