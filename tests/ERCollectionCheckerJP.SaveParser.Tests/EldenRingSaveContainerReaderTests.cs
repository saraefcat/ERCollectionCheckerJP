using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using ERCollectionCheckerJP.SaveParser.Container;

namespace ERCollectionCheckerJP.SaveParser.Tests;

public sealed class EldenRingSaveContainerReaderTests
{
    [Fact]
    public void ReadAndExtractPayload_ValidatesAllEntryChecksums()
    {
        var bytes = BuildContainer();

        var container = EldenRingSaveContainerReader.Read(bytes);

        Assert.Equal("00000001", container.BinderVersion);
        Assert.Equal(12, container.Entries.Count);
        for (var index = 0; index < container.Entries.Count; index++)
        {
            Assert.Equal(
                BitConverter.GetBytes(index + 1),
                EldenRingSaveContainerReader.ExtractPayload(bytes, container.Entries[index]));
        }
    }

    [Fact]
    public void ExtractPayload_RejectsChecksumMismatch()
    {
        var bytes = BuildContainer();
        var container = EldenRingSaveContainerReader.Read(bytes);
        bytes[container.Entries[0].DataOffset + 0x10] ^= 0xFF;

        Assert.Throws<InvalidDataException>(() =>
            EldenRingSaveContainerReader.ExtractPayload(bytes, container.Entries[0]));
    }

    private static byte[] BuildContainer()
    {
        const int headerEnd = 0x300;
        const int storedSize = 0x14;
        var bytes = new byte[headerEnd + (12 * storedSize)];
        "BND4"u8.CopyTo(bytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x0C, 4), 12);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(0x10, 8), 0x40);
        Encoding.ASCII.GetBytes("00000001").CopyTo(bytes, 0x18);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(0x20, 8), 0x20);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(0x28, 8), headerEnd);
        bytes[0x30] = 1;
        bytes[0x31] = 0x20;

        var nameOffset = 0x1C0;
        var dataOffset = headerEnd;
        for (var index = 0; index < 12; index++)
        {
            var headerOffset = 0x40 + (index * 0x20);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(headerOffset, 4), 0x50);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(headerOffset + 4, 4), -1);
            BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(headerOffset + 8, 8), storedSize);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(headerOffset + 0x10, 4), dataOffset);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(headerOffset + 0x14, 4), nameOffset);

            var name = Encoding.Unicode.GetBytes($"USER_DATA{index:D3}\0");
            name.CopyTo(bytes, nameOffset);
            nameOffset += name.Length;

            var payload = BitConverter.GetBytes(index + 1);
            MD5.HashData(payload).CopyTo(bytes, dataOffset);
            payload.CopyTo(bytes, dataOffset + 0x10);
            dataOffset += storedSize;
        }

        return bytes;
    }
}
