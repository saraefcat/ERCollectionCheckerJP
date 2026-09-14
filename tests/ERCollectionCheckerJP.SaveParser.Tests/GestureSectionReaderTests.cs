using System.Buffers.Binary;
using ERCollectionCheckerJP.SaveParser.Items;

namespace ERCollectionCheckerJP.SaveParser.Tests;

public sealed class GestureSectionReaderTests
{
    private const int StartOffset = 48;

    [Fact]
    public void ReadSlot_ReadsOddCanonicalIdsAndSkipsEmptySlots()
    {
        var slot = CreateSlot((0, 1u), (12, 41u), (63, 233u));

        var result = GestureSectionReader.ReadSlot(slot, Storage());

        Assert.Equal(StartOffset, result.StartOffset);
        Assert.Equal(StartOffset + GestureSectionReader.SectionSize, result.EndOffset);
        Assert.Equal([1u, 41u, 233u], result.Records.Select(static record => record.SaveGestureId));
        Assert.Equal([0, 12, 63], result.Records.Select(static record => record.Index));
    }

    [Fact]
    public void ReadSlot_AcceptsAllEmptySlots()
    {
        var result = GestureSectionReader.ReadSlot(CreateSlot(), Storage());

        Assert.Empty(result.Records);
    }

    [Fact]
    public void ReadSlot_PreservesUnknownIdsForDatabaseResolution()
    {
        var slot = CreateSlot();
        BinaryPrimitives.WriteUInt32LittleEndian(
            slot.AsSpan(StartOffset, GestureSectionReader.RecordSize),
            110);

        var result = GestureSectionReader.ReadSlot(slot, Storage());

        Assert.Equal(110u, Assert.Single(result.Records).SaveGestureId);
    }

    [Fact]
    public void ReadSlot_RejectsInvalidMaximumValue()
    {
        var slot = CreateSlot();
        BinaryPrimitives.WriteUInt32LittleEndian(
            slot.AsSpan(StartOffset, GestureSectionReader.RecordSize),
            uint.MaxValue);

        Assert.Throws<InvalidDataException>(() =>
            GestureSectionReader.ReadSlot(slot, Storage()));
    }

    [Fact]
    public void ReadSlot_RejectsDuplicateId()
    {
        var slot = CreateSlot((0, 1u), (1, 1u));

        Assert.Throws<InvalidDataException>(() =>
            GestureSectionReader.ReadSlot(slot, Storage()));
    }

    [Fact]
    public void ReadSlot_RejectsTruncatedSection()
    {
        var slot = new byte[StartOffset + GestureSectionReader.SectionSize - 1];

        Assert.Throws<InvalidDataException>(() =>
            GestureSectionReader.ReadSlot(slot, Storage()));
    }

    private static byte[] CreateSlot(params (int Index, uint SaveGestureId)[] gestures)
    {
        var slot = new byte[StartOffset + GestureSectionReader.SectionSize];
        for (var index = 0; index < GestureSectionReader.SlotCount; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(
                slot.AsSpan(
                    StartOffset + (index * GestureSectionReader.RecordSize),
                    GestureSectionReader.RecordSize),
                GestureSectionReader.EmptyGestureId);
        }

        foreach (var gesture in gestures)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(
                slot.AsSpan(
                    StartOffset + (gesture.Index * GestureSectionReader.RecordSize),
                    GestureSectionReader.RecordSize),
                gesture.SaveGestureId);
        }

        return slot;
    }

    private static StorageSection Storage() => new(
        0,
        0,
        StartOffset,
        0,
        0,
        0,
        0,
        0,
        []);
}
