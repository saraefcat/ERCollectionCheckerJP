using ERCollectionCheckerJP.Domain.Items;
using ERCollectionCheckerJP.ItemDatabase.DataPacks;

namespace ERCollectionCheckerJP.ItemDatabase.Catalog;

public sealed class GestureMappingIndex
{
    private readonly IReadOnlyDictionary<uint, ItemIdentity> identitiesBySaveGestureId;

    private GestureMappingIndex(IReadOnlyDictionary<uint, ItemIdentity> identitiesBySaveGestureId)
    {
        this.identitiesBySaveGestureId = identitiesBySaveGestureId;
    }

    public int MappingCount => identitiesBySaveGestureId.Count;

    public bool TryResolve(uint saveGestureId, out ItemIdentity identity) =>
        identitiesBySaveGestureId.TryGetValue(saveGestureId, out identity);

    public static GestureMappingIndex Create(IEnumerable<GestureMappingEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return new GestureMappingIndex(entries.ToDictionary(
            static entry => entry.SaveGestureId,
            static entry => new ItemIdentity(ItemKind.Goods, entry.ItemParamId)));
    }
}
