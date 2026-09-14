using System.Buffers.Binary;
using ERCollectionCheckerJP.SaveParser.Items;

namespace ERCollectionCheckerJP.SaveParser.Tests;

public sealed class EquippedSpellSectionReaderTests
{
    private const int StartOffset = 32;

    [Fact]
    public void ReadSlot_ReadsOccupiedSlotsAndSelectedIndex()
    {
        var slot = CreateSlot((0, 6050u), (4, 4670u));
        BinaryPrimitives.WriteInt32LittleEndian(
            slot.AsSpan(StartOffset + EquippedSpellSectionReader.SectionSize - sizeof(int)),
            4);

        var result = EquippedSpellSectionReader.ReadSlot(slot, Inventory());

        Assert.Equal(StartOffset, result.StartOffset);
        Assert.Equal(StartOffset + EquippedSpellSectionReader.SectionSize, result.EndOffset);
        Assert.Equal(4, result.SelectedSlotIndex);
        Assert.Collection(
            result.Records,
            first =>
            {
                Assert.Equal(0, first.Index);
                Assert.Equal(6050u, first.SpellId);
            },
            second =>
            {
                Assert.Equal(4, second.Index);
                Assert.Equal(4670u, second.SpellId);
            });
    }

    [Fact]
    public void ReadSlot_AcceptsEmptySectionWithNoSelection()
    {
        var result = EquippedSpellSectionReader.ReadSlot(CreateSlot(), Inventory());

        Assert.Empty(result.Records);
        Assert.Equal(-1, result.SelectedSlotIndex);
    }

    [Fact]
    public void ReadSlot_RejectsPartiallyEmptyRecord()
    {
        var slot = CreateSlot();
        BinaryPrimitives.WriteUInt32LittleEndian(slot.AsSpan(StartOffset, sizeof(uint)), uint.MaxValue);
        BinaryPrimitives.WriteUInt32LittleEndian(slot.AsSpan(StartOffset + sizeof(uint), sizeof(uint)), uint.MaxValue);

        Assert.Throws<InvalidDataException>(() =>
            EquippedSpellSectionReader.ReadSlot(slot, Inventory()));
    }

    [Fact]
    public void ReadSlot_RejectsSelectedEmptySlot()
    {
        var slot = CreateSlot((0, 6050u));
        BinaryPrimitives.WriteInt32LittleEndian(
            slot.AsSpan(StartOffset + EquippedSpellSectionReader.SectionSize - sizeof(int)),
            1);

        Assert.Throws<InvalidDataException>(() =>
            EquippedSpellSectionReader.ReadSlot(slot, Inventory()));
    }

    [Fact]
    public void ReadSlot_RejectsTruncatedSection()
    {
        var slot = new byte[StartOffset + EquippedSpellSectionReader.SectionSize - 1];

        Assert.Throws<InvalidDataException>(() =>
            EquippedSpellSectionReader.ReadSlot(slot, Inventory()));
    }

    private static byte[] CreateSlot(params (int Index, uint SpellId)[] spells)
    {
        var slot = new byte[StartOffset + EquippedSpellSectionReader.SectionSize];
        for (var index = 0; index < EquippedSpellSectionReader.SlotCount; index++)
        {
            var offset = StartOffset + (index * EquippedSpellSectionReader.RecordSize);
            BinaryPrimitives.WriteUInt32LittleEndian(slot.AsSpan(offset, sizeof(uint)), uint.MaxValue);
        }

        foreach (var spell in spells)
        {
            var offset = StartOffset + (spell.Index * EquippedSpellSectionReader.RecordSize);
            BinaryPrimitives.WriteUInt32LittleEndian(slot.AsSpan(offset, sizeof(uint)), spell.SpellId);
            BinaryPrimitives.WriteUInt32LittleEndian(
                slot.AsSpan(offset + sizeof(uint), sizeof(uint)),
                uint.MaxValue);
        }

        BinaryPrimitives.WriteInt32LittleEndian(
            slot.AsSpan(StartOffset + EquippedSpellSectionReader.SectionSize - sizeof(int)),
            -1);
        return slot;
    }

    private static InventorySection Inventory() => new(
        0,
        0,
        StartOffset,
        0,
        0,
        0,
        0,
        []);
}
