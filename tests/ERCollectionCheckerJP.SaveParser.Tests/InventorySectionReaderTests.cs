using System.Buffers.Binary;
using ERCollectionCheckerJP.Domain.Items;
using ERCollectionCheckerJP.SaveParser.Items;

namespace ERCollectionCheckerJP.SaveParser.Tests;

public sealed class InventorySectionReaderTests
{
    private const int GaItemEnd = 0x40;
    private const int AnchorOffset = GaItemEnd + 0x1B0 - 1;
    private const int InventoryStart = AnchorOffset + 505;

    [Fact]
    public void ReadSlot_ResolvesMappedAndDirectHandleItems()
    {
        var slot = CreateSlot();
        var gaItems = new GaItemSection(
            0x20,
            GaItemEnd,
            [],
            new Dictionary<uint, uint>
            {
                [0x8000_0001] = 8_530_107,
                [0x9000_0002] = 0x1051_79B4,
            });
        WriteRecord(slot, InventoryStart, 0x8000_0001, 1, 500);
        WriteRecord(slot, InventoryStart + 12, 0xA000_03FE, 1, 501);
        WriteRecord(slot, InventoryStart + 24, 0xB000_0073, 12, 502);
        var keyStart = InventoryStart + (InventorySectionReader.CommonItemCapacity * 12) + 4;
        WriteRecord(slot, keyStart, 0x9000_0002, 1, 503);

        var result = InventorySectionReader.ReadSlot(slot, gaItems);

        Assert.Equal(InventoryStart, result.StartOffset);
        Assert.Collection(
            result.Records,
            weapon =>
            {
                Assert.Equal(ItemKind.Weapon, weapon.Kind);
                Assert.Equal(8_530_107u, weapon.ParamId);
                Assert.Equal(InventoryItemResolution.GaItemMap, weapon.Resolution);
            },
            accessory =>
            {
                Assert.Equal(ItemKind.Accessory, accessory.Kind);
                Assert.Equal(1_022u, accessory.ParamId);
                Assert.Equal(InventoryItemResolution.EncodedInHandle, accessory.Resolution);
            },
            goods =>
            {
                Assert.Equal(ItemKind.Goods, goods.Kind);
                Assert.Equal(115u, goods.ParamId);
                Assert.Equal(12u, goods.Quantity);
            },
            armor =>
            {
                Assert.Equal(InventoryContainer.Key, armor.Container);
                Assert.Equal(5_339_572u, armor.ParamId);
            });
    }

    [Fact]
    public void ReadSlot_LeavesInstanceHandleUnresolvedWhenGaItemIsMissing()
    {
        var slot = CreateSlot();
        var gaItems = new GaItemSection(0x20, GaItemEnd, [], new Dictionary<uint, uint>());
        WriteRecord(slot, InventoryStart, 0xC000_0010, 1, 500);

        var result = InventorySectionReader.ReadSlot(slot, gaItems);

        var item = Assert.Single(result.Records);
        Assert.Equal(ItemKind.AshOfWar, item.Kind);
        Assert.Null(item.RawItemId);
        Assert.Null(item.ParamId);
        Assert.Equal(InventoryItemResolution.Unresolved, item.Resolution);
    }

    [Fact]
    public void ReadSlot_RejectsMissingBoundaryMarker()
    {
        var slot = new byte[InventoryStart + 0x9010];
        var gaItems = new GaItemSection(0x20, GaItemEnd, [], new Dictionary<uint, uint>());

        var exception = Assert.Throws<InvalidDataException>(() =>
            InventorySectionReader.ReadSlot(slot, gaItems));

        Assert.Contains("boundary marker", exception.Message, StringComparison.Ordinal);
    }

    private static byte[] CreateSlot()
    {
        var slot = new byte[InventoryStart + 0x9010];
        ReadOnlySpan<byte> pattern =
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
        pattern.CopyTo(slot.AsSpan(AnchorOffset));
        return slot;
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
