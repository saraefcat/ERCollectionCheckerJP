using System.Buffers.Binary;
using System.Text;
using ERCollectionCheckerJP.SaveParser.Character;
using ERCollectionCheckerJP.SaveParser.Items;

namespace ERCollectionCheckerJP.SaveParser.Tests;

public sealed class CharacterSlotMetadataReaderTests
{
    private const int LegacyGaItemCount = 5_118;
    private const int EmptyGaItemSize = 8;

    [Fact]
    public void ReadSlot_ReturnsEmptyMetadataForVersionZero()
    {
        var metadata = CharacterSlotMetadataReader.ReadSlot(new byte[sizeof(uint)]);

        Assert.True(metadata.IsEmpty);
        Assert.Equal(0u, metadata.SlotVersion);
        Assert.Null(metadata.CharacterName);
    }

    [Theory]
    [InlineData("Test Hero")]
    [InlineData("Test Hero NG4")]
    [InlineData("テスト勇者")]
    public void ReadSlot_ReadsAValidatedUtf16NameRelativeToTheGaItemEnd(string name)
    {
        var slot = CreateOccupiedSlot(name);

        var metadata = CharacterSlotMetadataReader.ReadSlot(slot);

        Assert.False(metadata.IsEmpty);
        Assert.Equal(1u, metadata.SlotVersion);
        Assert.Equal(name, metadata.CharacterName);
    }

    [Fact]
    public void ReadSlot_AllowsTheMaximumSixteenCharacters()
    {
        const string name = "1234567890ABCDEF";
        var metadata = CharacterSlotMetadataReader.ReadSlot(CreateOccupiedSlot(name));

        Assert.Equal(name, metadata.CharacterName);
    }

    [Fact]
    public void ReadSlot_RejectsAnUnterminatedName()
    {
        var slot = CreateOccupiedSlot("1234567890ABCDEF");
        var gaItemEnd = GaItemSectionReader.StartOffset + (LegacyGaItemCount * EmptyGaItemSize);
        var terminatorOffset = gaItemEnd +
            CharacterSlotMetadataReader.CharacterNameOffsetFromPlayerGameData +
            (CharacterSlotMetadataReader.MaximumCharacterNameLength * sizeof(ushort));
        BinaryPrimitives.WriteUInt16LittleEndian(slot.AsSpan(terminatorOffset), 'X');

        Assert.Throws<InvalidDataException>(() =>
            CharacterSlotMetadataReader.ReadSlot(slot));
    }

    [Fact]
    public void ReadSlot_RejectsControlCharacters()
    {
        var slot = CreateOccupiedSlot("Bad\nName");

        Assert.Throws<InvalidDataException>(() =>
            CharacterSlotMetadataReader.ReadSlot(slot));
    }

    private static byte[] CreateOccupiedSlot(string name)
    {
        if (name.Length > CharacterSlotMetadataReader.MaximumCharacterNameLength)
        {
            throw new ArgumentOutOfRangeException(nameof(name));
        }

        var gaItemEnd = GaItemSectionReader.StartOffset + (LegacyGaItemCount * EmptyGaItemSize);
        var nameOffset = gaItemEnd + CharacterSlotMetadataReader.CharacterNameOffsetFromPlayerGameData;
        var slot = new byte[nameOffset +
            ((CharacterSlotMetadataReader.MaximumCharacterNameLength + 1) * sizeof(ushort))];
        BinaryPrimitives.WriteUInt32LittleEndian(slot, 1);
        for (var index = 0; index < LegacyGaItemCount; index++)
        {
            var offset = GaItemSectionReader.StartOffset + (index * EmptyGaItemSize);
            BinaryPrimitives.WriteUInt32LittleEndian(slot.AsSpan(offset), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(slot.AsSpan(offset + sizeof(uint)), uint.MaxValue);
        }

        Encoding.Unicode.GetBytes(name).CopyTo(slot, nameOffset);
        return slot;
    }
}
