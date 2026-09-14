using System.Buffers.Binary;
using ERCollectionCheckerJP.Domain.Items;
using ERCollectionCheckerJP.SaveParser.Items;

namespace ERCollectionCheckerJP.SaveParser.Tests;

public sealed class GaItemSectionReaderTests
{
    [Fact]
    public void Read_AdvancesByTypeSpecificRecordSizes()
    {
        var bytes = new byte[0x20 + 21 + 16 + 8 + 8];
        var offset = 0x20;
        WriteUInt32(bytes, offset, 0x8000_0001);
        WriteUInt32(bytes, offset + 4, 100_005);
        WriteInt32(bytes, offset + 8, -1);
        WriteInt32(bytes, offset + 12, -1);
        WriteUInt32(bytes, offset + 16, 0xC000_0123);
        bytes[offset + 20] = 7;
        offset += 21;

        WriteUInt32(bytes, offset, 0x9000_0002);
        WriteUInt32(bytes, offset + 4, 0x1012_3456);
        WriteInt32(bytes, offset + 8, -1);
        WriteInt32(bytes, offset + 12, -1);
        offset += 16;

        WriteUInt32(bytes, offset, 0xC000_0003);
        WriteUInt32(bytes, offset + 4, 0x8000_5654);
        offset += 8;

        WriteUInt32(bytes, offset, 0);
        WriteUInt32(bytes, offset + 4, uint.MaxValue);

        var result = GaItemSectionReader.Read(bytes, 4);

        Assert.Equal(bytes.Length, result.EndOffset);
        Assert.Equal(3, result.HandleToRawItemId.Count);
        Assert.Collection(
            result.Records,
            item =>
            {
                Assert.Equal(ItemKind.Weapon, item.Kind);
                Assert.Equal(100_005u, item.ParamId);
                Assert.Equal(0xC000_0123u, item.AttachedAshOfWarHandle);
            },
            item => Assert.Equal(ItemKind.Armor, item.Kind),
            item =>
            {
                Assert.Equal(ItemKind.AshOfWar, item.Kind);
                Assert.Equal(22_100u, item.ParamId);
            },
            item => Assert.Null(item.ParamId));
    }

    [Theory]
    [InlineData(0x00000001u, ItemKind.Weapon)]
    [InlineData(0x10000001u, ItemKind.Armor)]
    [InlineData(0x20000001u, ItemKind.Accessory)]
    [InlineData(0x40000001u, ItemKind.Goods)]
    [InlineData(0x80000001u, ItemKind.AshOfWar)]
    public void DecodeKind_MapsRawItemPrefixes(uint rawItemId, ItemKind expected) =>
        Assert.Equal(expected, GaItemSectionReader.DecodeKind(rawItemId));

    private static void WriteUInt32(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);

    private static void WriteInt32(byte[] bytes, int offset, int value) =>
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, 4), value);
}
