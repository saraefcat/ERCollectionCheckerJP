using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using ERCollectionCheckerJP.SaveParser.Items;

namespace ERCollectionCheckerJP.SaveParser.Character;

public sealed record CharacterSlotMetadata(
    uint SlotVersion,
    bool IsEmpty,
    string? CharacterName);

/// <summary>
/// Reads the minimal, verified character-selection metadata from one character
/// payload. The name address is resolved from the variable GaItem end instead
/// of from an absolute save-file offset.
/// </summary>
public static class CharacterSlotMetadataReader
{
    public const int CharacterNameOffsetFromPlayerGameData = 0x94;
    public const int MaximumCharacterNameLength = 16;

    private static readonly Encoding StrictUtf16 = new UnicodeEncoding(
        bigEndian: false,
        byteOrderMark: false,
        throwOnInvalidBytes: true);

    public static CharacterSlotMetadata ReadSlot(ReadOnlySpan<byte> slot)
    {
        if (slot.Length < sizeof(uint))
        {
            throw new InvalidDataException("Save slot is shorter than the version field.");
        }

        var version = BinaryPrimitives.ReadUInt32LittleEndian(slot);
        if (version == 0)
        {
            return new CharacterSlotMetadata(version, true, null);
        }

        var gaItems = GaItemSectionReader.ReadSlot(slot);
        var nameOffset = checked(gaItems.EndOffset + CharacterNameOffsetFromPlayerGameData);
        var requiredBytes = checked((MaximumCharacterNameLength + 1) * sizeof(ushort));
        if (nameOffset < 0 || nameOffset > slot.Length - requiredBytes)
        {
            throw new InvalidDataException("Character name exceeds the save slot.");
        }

        var length = -1;
        for (var index = 0; index <= MaximumCharacterNameLength; index++)
        {
            var codeUnit = BinaryPrimitives.ReadUInt16LittleEndian(
                slot.Slice(nameOffset + (index * sizeof(ushort)), sizeof(ushort)));
            if (codeUnit == 0)
            {
                length = index;
                break;
            }
        }

        if (length is <= 0 or > MaximumCharacterNameLength)
        {
            throw new InvalidDataException(
                "Character name is empty or is not terminated within the supported length.");
        }

        string name;
        try
        {
            name = StrictUtf16.GetString(slot.Slice(nameOffset, length * sizeof(ushort)));
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("Character name contains invalid UTF-16.", exception);
        }

        if (!string.Equals(name, name.Trim(), StringComparison.Ordinal) ||
            name.EnumerateRunes().Any(static rune => IsUnsupported(Rune.GetUnicodeCategory(rune))))
        {
            throw new InvalidDataException("Character name contains unsupported characters.");
        }

        return new CharacterSlotMetadata(version, false, name);
    }

    private static bool IsUnsupported(UnicodeCategory category) => category is
        UnicodeCategory.Control or
        UnicodeCategory.Format or
        UnicodeCategory.Surrogate or
        UnicodeCategory.PrivateUse or
        UnicodeCategory.OtherNotAssigned or
        UnicodeCategory.LineSeparator or
        UnicodeCategory.ParagraphSeparator;
}
