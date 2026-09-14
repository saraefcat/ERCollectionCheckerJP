using System.Buffers.Binary;
using ERCollectionCheckerJP.SaveParser.Flags;
using ERCollectionCheckerJP.SaveParser.Items;

namespace ERCollectionCheckerJP.SaveParser.Tests;

public sealed class EventFlagsSectionReaderTests
{
    [Fact]
    public void ReadSlot_FollowsDynamicCountsAndReadsBigEndianFlagAddress()
    {
        const int inventoryEnd = 0xA000;
        const int projectileCount = 3;
        const int regionCount = 2;
        var projectileHeader = inventoryEnd + 0x74 + 0x8C + 0x18;
        var storageStart = projectileHeader + 4 + (projectileCount * 8) + 0x9C + 0x0C + 0x12F;
        var regionHeader = storageStart + 0x6010 + 0x100;
        var eventStart = regionHeader + 4 + (regionCount * 4) +
            0x29 + 0x4C + 0x103C + 0x1B588 + 0x40B + 0x1A;
        var slot = new byte[eventStart + EventFlagsSectionReader.FlagByteCount + 1];
        BinaryPrimitives.WriteUInt32LittleEndian(slot.AsSpan(projectileHeader, 4), projectileCount);
        BinaryPrimitives.WriteUInt32LittleEndian(slot.AsSpan(regionHeader, 4), regionCount);
        slot[eventStart + 1262] = 0b0000_0100;
        var storage = new StorageSection(
            storageStart,
            storageStart + 4,
            storageStart + 0x6010,
            projectileCount,
            0,
            0,
            0,
            0,
            []);

        var section = EventFlagsSectionReader.ReadSlot(slot, storage);
        var set = EventFlagsSectionReader.IsSet(
            slot,
            section,
            new EventFlagAddress(60_101, 1262, 2));
        var clear = EventFlagsSectionReader.IsSet(
            slot,
            section,
            new EventFlagAddress(60_102, 1262, 1));

        Assert.Equal(eventStart, section.StartOffset);
        Assert.Equal(projectileCount, section.ProjectileCount);
        Assert.Equal(regionCount, section.UnlockedRegionCount);
        Assert.True(set);
        Assert.False(clear);
    }

    [Fact]
    public void IsSet_RejectsAddressOutsideFlagBlock()
    {
        var section = new EventFlagsSection(0, 0, 0, 0);
        var address = new EventFlagAddress(1, EventFlagsSectionReader.FlagByteCount, 0);

        Assert.Throws<InvalidDataException>(() =>
            EventFlagsSectionReader.IsSet([], section, address));
    }
}
