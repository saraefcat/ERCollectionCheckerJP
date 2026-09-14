using System.Buffers.Binary;

namespace ERCollectionCheckerJP.SaveParser.Items;

public sealed record EquippedSpellRecord(
    int Index,
    int Offset,
    uint SpellId,
    uint OccupiedSentinel);

public sealed record EquippedSpellSection(
    int StartOffset,
    int EndOffset,
    int SelectedSlotIndex,
    IReadOnlyList<EquippedSpellRecord> Records);

/// <summary>
/// Reads the fixed 14-slot equipped-spell section immediately following held inventory.
/// Spell IDs are MagicParam IDs, not GaItem handles.
/// </summary>
public static class EquippedSpellSectionReader
{
    public const int SlotCount = 14;
    public const int RecordSize = sizeof(uint) * 2;
    public const int SectionSize = (SlotCount * RecordSize) + sizeof(int);

    private const uint EmptySpellId = uint.MaxValue;
    private const uint OccupiedSentinel = uint.MaxValue;

    public static EquippedSpellSection ReadSlot(
        ReadOnlySpan<byte> slot,
        InventorySection inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        var startOffset = inventory.EndOffset;
        var endOffset = checked(startOffset + SectionSize);
        EnsureAvailable(slot, startOffset, SectionSize);

        var records = new List<EquippedSpellRecord>(SlotCount);
        for (var index = 0; index < SlotCount; index++)
        {
            var offset = checked(startOffset + (index * RecordSize));
            var spellId = BinaryPrimitives.ReadUInt32LittleEndian(
                slot.Slice(offset, sizeof(uint)));
            var occupiedSentinel = BinaryPrimitives.ReadUInt32LittleEndian(
                slot.Slice(offset + sizeof(uint), sizeof(uint)));
            if (spellId == EmptySpellId && occupiedSentinel == 0)
            {
                continue;
            }

            if (spellId is 0 or EmptySpellId || occupiedSentinel != OccupiedSentinel)
            {
                throw new InvalidDataException($"Equipped spell slot {index} is invalid.");
            }

            records.Add(new EquippedSpellRecord(index, offset, spellId, occupiedSentinel));
        }

        var selectedSlotIndex = BinaryPrimitives.ReadInt32LittleEndian(
            slot.Slice(endOffset - sizeof(int), sizeof(int)));
        if (selectedSlotIndex is < -1 or >= SlotCount ||
            (selectedSlotIndex >= 0 && records.All(record => record.Index != selectedSlotIndex)))
        {
            throw new InvalidDataException("The selected equipped-spell slot is invalid.");
        }

        return new EquippedSpellSection(startOffset, endOffset, selectedSlotIndex, records);
    }

    private static void EnsureAvailable(ReadOnlySpan<byte> bytes, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length - length)
        {
            throw new InvalidDataException("The equipped-spell range exceeds the save slot.");
        }
    }
}
