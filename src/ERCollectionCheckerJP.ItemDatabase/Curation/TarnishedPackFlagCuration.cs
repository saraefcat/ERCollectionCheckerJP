using System.Text.Json;

namespace ERCollectionCheckerJP.ItemDatabase.Curation;

public sealed record TarnishedPackFlagCuration(
    int SchemaVersion,
    string GameVersion,
    string Status,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<TarnishedPackFlagCurationEntry> Flags);

public sealed record TarnishedPackFlagCurationEntry(
    string ItemKey,
    int ItemLotParamId,
    uint FlagId,
    int ByteIndex,
    byte BitIndex);

public static class TarnishedPackFlagCurationReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static TarnishedPackFlagCuration Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var result = JsonSerializer.Deserialize<TarnishedPackFlagCuration>(stream, JsonOptions) ??
            throw new InvalidDataException("Tarnished Pack flag curation JSON is empty.");
        Validate(result);
        return result;
    }

    private static void Validate(TarnishedPackFlagCuration value)
    {
        if (value.SchemaVersion != 1 ||
            !string.Equals(value.GameVersion, "1.17", StringComparison.Ordinal) ||
            !string.Equals(value.Status, "reviewed", StringComparison.Ordinal) ||
            value.Evidence.Count == 0 ||
            value.Flags.Count != 3)
        {
            throw new InvalidDataException("Unsupported or unreviewed Tarnished Pack flag data.");
        }

        var itemKeys = new HashSet<string>(StringComparer.Ordinal);
        var flagIds = new HashSet<uint>();
        foreach (var flag in value.Flags)
        {
            if (!flag.ItemKey.StartsWith("torrentattire:", StringComparison.Ordinal) ||
                flag.ItemLotParamId <= 0 ||
                flag.FlagId == 0 ||
                flag.ByteIndex < 0 ||
                flag.BitIndex > 7 ||
                !itemKeys.Add(flag.ItemKey) ||
                !flagIds.Add(flag.FlagId))
            {
                throw new InvalidDataException("Tarnished Pack flag curation contains an invalid entry.");
            }
        }
    }
}
