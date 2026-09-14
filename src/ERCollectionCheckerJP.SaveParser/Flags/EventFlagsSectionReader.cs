using System.Buffers.Binary;
using ERCollectionCheckerJP.SaveParser.Items;

namespace ERCollectionCheckerJP.SaveParser.Flags;

public sealed record EventFlagAddress(
    uint FlagId,
    int ByteIndex,
    byte BitIndex);

public sealed record EventFlagsSection(
    int StartOffset,
    int EndOffset,
    int ProjectileCount,
    int UnlockedRegionCount);

/// <summary>
/// Locates and reads the fixed-size event-flag bitfield by following the
/// validated, variable-length slot sections that precede it.
/// </summary>
public static class EventFlagsSectionReader
{
    public const int FlagByteCount = 0x1BF99F;
    private const int TerminatorSize = 1;
    private const int MaximumDynamicEntryCount = 4096;

    public static EventFlagsSection ReadSlot(
        ReadOnlySpan<byte> slot,
        StorageSection storage)
    {
        ArgumentNullException.ThrowIfNull(storage);

        var unlockedRegionCountOffset = checked(storage.EndOffset + 0x100);
        var unlockedRegionCount = ReadBoundedCount(
            slot,
            unlockedRegionCountOffset,
            "unlocked region");
        var afterUnlockedRegions = checked(
            unlockedRegionCountOffset + sizeof(uint) + (unlockedRegionCount * sizeof(uint)));

        var startOffset = checked(
            afterUnlockedRegions +
            0x29 +
            0x4C +
            0x103C +
            0x1B588 +
            0x40B +
            0x1A);
        var endOffset = checked(startOffset + FlagByteCount + TerminatorSize);
        EnsureAvailable(slot, startOffset, FlagByteCount + TerminatorSize, "event flags");
        return new EventFlagsSection(
            startOffset,
            endOffset,
            storage.ProjectileCount,
            unlockedRegionCount);
    }

    public static bool IsSet(
        ReadOnlySpan<byte> slot,
        EventFlagsSection section,
        EventFlagAddress address)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(address);
        if (address.ByteIndex < 0 || address.ByteIndex >= FlagByteCount || address.BitIndex > 7)
        {
            throw new InvalidDataException($"Event flag {address.FlagId} has an invalid address.");
        }

        EnsureAvailable(slot, section.StartOffset + address.ByteIndex, 1, "event flag");
        return (slot[section.StartOffset + address.ByteIndex] & (1 << address.BitIndex)) != 0;
    }

    private static int ReadBoundedCount(
        ReadOnlySpan<byte> slot,
        int offset,
        string section)
    {
        EnsureAvailable(slot, offset, sizeof(uint), section);
        var count = BinaryPrimitives.ReadUInt32LittleEndian(slot.Slice(offset, sizeof(uint)));
        if (count > MaximumDynamicEntryCount)
        {
            throw new InvalidDataException($"The {section} count is outside the supported range.");
        }

        return checked((int)count);
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
