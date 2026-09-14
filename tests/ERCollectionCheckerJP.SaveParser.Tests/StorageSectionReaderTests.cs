using System.Buffers.Binary;
using ERCollectionCheckerJP.Domain.Items;
using ERCollectionCheckerJP.SaveParser.Items;

namespace ERCollectionCheckerJP.SaveParser.Tests;

public sealed class StorageSectionReaderTests
{
    [Fact]
    public void ReadSlot_ParsesSparseCommonAndKeyRecords()
    {
        const int inventoryEnd = 0xA000;
        const int projectileCount = 7;
        var projectileHeader = inventoryEnd + 0x74 + 0x8C + 0x18;
        var storageHeader = projectileHeader + 4 + (projectileCount * 8) + 0x9C + 0x0C + 0x12F;
        var storageStart = storageHeader + 4;
        var keyStart = storageStart + (StorageSectionReader.CommonItemCapacity * 12) + 4;
        var slot = new byte[storageHeader + 0x6010];
        BinaryPrimitives.WriteUInt32LittleEndian(slot.AsSpan(projectileHeader, 4), projectileCount);
        WriteRecord(slot, storageStart + (12 * 3), 0xB000_0073, 9, 700);
        WriteRecord(slot, keyStart + (12 * 2), 0x9000_0002, 1, 701);
        var gaItems = new GaItemSection(
            0,
            0,
            [],
            new Dictionary<uint, uint> { [0x9000_0002] = 0x1051_79B4 });
        var inventory = new InventorySection(0, 0, inventoryEnd, 0, 0, 0, 0, []);

        var result = StorageSectionReader.ReadSlot(slot, gaItems, inventory);

        Assert.Equal(projectileCount, result.ProjectileCount);
        Assert.Equal(storageHeader + 0x6010, result.EndOffset);
        Assert.Collection(
            result.Records,
            goods =>
            {
                Assert.Equal(InventoryContainer.Common, goods.Container);
                Assert.Equal(3, goods.Index);
                Assert.Equal(ItemKind.Goods, goods.Kind);
                Assert.Equal(115u, goods.ParamId);
                Assert.Equal(9u, goods.Quantity);
            },
            armor =>
            {
                Assert.Equal(InventoryContainer.Key, armor.Container);
                Assert.Equal(2, armor.Index);
                Assert.Equal(ItemKind.Armor, armor.Kind);
                Assert.Equal(5_339_572u, armor.ParamId);
            });
    }

    private static void WriteRecord(
        byte[] slot,
        int offset,
        uint handle,
        uint quantity,
        uint acquisitionIndex)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(slot.AsSpan(offset, 4), handle);
        BinaryPrimitives.WriteUInt32LittleEndian(slot.AsSpan(offset + 4, 4), quantity);
        BinaryPrimitives.WriteUInt32LittleEndian(slot.AsSpan(offset + 8, 4), acquisitionIndex);
    }
}
