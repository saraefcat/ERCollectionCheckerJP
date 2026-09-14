using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace ERCollectionCheckerJP.SaveParser.Container;

public sealed record SaveContainerEntry(
    int Index,
    string Name,
    int DataOffset,
    int StoredSize,
    uint Flags);

public sealed record EldenRingSaveContainer(
    string BinderVersion,
    int HeaderSize,
    IReadOnlyList<SaveContainerEntry> Entries);

/// <summary>
/// Reads the unencrypted BND4 container currently written by the Steam version
/// of ELDEN RING. Every payload remains immutable and is verified against its
/// stored MD5 before being returned.
/// </summary>
public static class EldenRingSaveContainerReader
{
    private const int BinderHeaderSize = 0x40;
    private const int EntryHeaderSize = 0x20;
    private const int ExpectedHeaderEnd = 0x300;
    private const int ChecksumSize = 0x10;
    private const int ExpectedEntryCount = 12;

    public static EldenRingSaveContainer Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < ExpectedHeaderEnd || !bytes.StartsWith("BND4"u8))
        {
            throw new InvalidDataException("Save file is not an unencrypted BND4 container.");
        }

        var entryCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(0x0C, 4));
        var declaredHeaderSize = BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(0x10, 8));
        var binderVersion = Encoding.ASCII.GetString(bytes.Slice(0x18, 8)).TrimEnd('\0');
        var entryHeaderSize = BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(0x20, 8));
        var headersEnd = BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(0x28, 8));
        var unicodeNames = bytes[0x30] != 0;
        if (entryCount != ExpectedEntryCount ||
            declaredHeaderSize != BinderHeaderSize ||
            entryHeaderSize != EntryHeaderSize ||
            headersEnd != ExpectedHeaderEnd ||
            !unicodeNames)
        {
            throw new InvalidDataException("Save BND4 header layout is unsupported.");
        }

        var entries = new List<SaveContainerEntry>(ExpectedEntryCount);
        var expectedDataOffset = ExpectedHeaderEnd;
        for (var index = 0; index < ExpectedEntryCount; index++)
        {
            var headerOffset = BinderHeaderSize + (index * EntryHeaderSize);
            var header = bytes.Slice(headerOffset, EntryHeaderSize);
            var flags = BinaryPrimitives.ReadUInt32LittleEndian(header[..4]);
            var id = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(0x04, 4));
            var storedSize64 = BinaryPrimitives.ReadInt64LittleEndian(header.Slice(0x08, 8));
            var dataOffset = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(0x10, 4));
            var nameOffset = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(0x14, 4));
            var tail = BinaryPrimitives.ReadInt64LittleEndian(header.Slice(0x18, 8));
            if (flags != 0x50 || id != -1 || tail != 0 ||
                storedSize64 is < ChecksumSize or > int.MaxValue)
            {
                throw new InvalidDataException($"Save BND4 entry {index} header is unsupported.");
            }

            var storedSize = checked((int)storedSize64);
            if (dataOffset != expectedDataOffset || dataOffset > bytes.Length - storedSize)
            {
                throw new InvalidDataException($"Save BND4 entry {index} data range is invalid.");
            }

            var name = ReadName(bytes, nameOffset, index);
            var expectedName = $"USER_DATA{index:D3}";
            if (!string.Equals(name, expectedName, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Save BND4 entry {index} has unexpected name {name}.");
            }

            entries.Add(new SaveContainerEntry(index, name, dataOffset, storedSize, flags));
            expectedDataOffset = checked(dataOffset + storedSize);
        }

        if (expectedDataOffset != bytes.Length)
        {
            throw new InvalidDataException("Save BND4 entries do not cover the complete file.");
        }

        return new EldenRingSaveContainer(binderVersion, ExpectedHeaderEnd, entries);
    }

    public static byte[] ExtractPayload(
        ReadOnlySpan<byte> bytes,
        SaveContainerEntry entry,
        bool validateChecksum = true)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.DataOffset < 0 || entry.StoredSize < ChecksumSize ||
            entry.DataOffset > bytes.Length - entry.StoredSize)
        {
            throw new InvalidDataException("Save entry range is outside the container.");
        }

        var stored = bytes.Slice(entry.DataOffset, entry.StoredSize);
        var payload = stored[ChecksumSize..];
        if (validateChecksum)
        {
            Span<byte> actual = stackalloc byte[ChecksumSize];
            MD5.HashData(payload, actual);
            if (!CryptographicOperations.FixedTimeEquals(stored[..ChecksumSize], actual))
            {
                throw new InvalidDataException($"Save entry {entry.Name} failed its MD5 check.");
            }
        }

        return payload.ToArray();
    }

    private static string ReadName(ReadOnlySpan<byte> bytes, int offset, int index)
    {
        if (offset < BinderHeaderSize + (ExpectedEntryCount * EntryHeaderSize) ||
            offset >= ExpectedHeaderEnd || (offset & 1) != 0)
        {
            throw new InvalidDataException($"Save BND4 entry {index} name offset is invalid.");
        }

        var end = offset;
        while (end <= ExpectedHeaderEnd - sizeof(ushort))
        {
            if (BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(end, 2)) == 0)
            {
                return Encoding.Unicode.GetString(bytes.Slice(offset, end - offset));
            }

            end += sizeof(ushort);
        }

        throw new InvalidDataException($"Save BND4 entry {index} name is not terminated.");
    }
}
