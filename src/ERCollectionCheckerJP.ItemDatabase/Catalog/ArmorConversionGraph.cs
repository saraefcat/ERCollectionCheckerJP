using ERCollectionCheckerJP.Domain.Items;

namespace ERCollectionCheckerJP.ItemDatabase.Catalog;

public sealed record ArmorConversionEntry(
    string ItemKey,
    uint ParamId,
    string ArmorVariantId,
    ArmorVariantType VariantType,
    string AlterationFamilyId,
    bool DirectlyObtainable,
    ArmorAcquisitionKind AcquisitionKind,
    IReadOnlyList<uint> TransformTargets);

/// <summary>
/// A reviewed, directed armor-conversion graph. Every edge must be present in the supplied data;
/// reverse edges and name-based relationships are never inferred.
/// </summary>
public sealed class ArmorConversionGraph
{
    private readonly IReadOnlyDictionary<string, ArmorConversionEntry> entriesByKey;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> sourcesByTargetKey;

    private ArmorConversionGraph(
        IReadOnlyList<ArmorConversionEntry> entries,
        IReadOnlyDictionary<string, ArmorConversionEntry> entriesByKey,
        IReadOnlyDictionary<string, IReadOnlyList<string>> sourcesByTargetKey)
    {
        Entries = entries;
        this.entriesByKey = entriesByKey;
        this.sourcesByTargetKey = sourcesByTargetKey;
    }

    public IReadOnlyList<ArmorConversionEntry> Entries { get; }

    public static ArmorConversionGraph Create(
        IEnumerable<ArmorConversionEntry> entries,
        IEnumerable<ReviewedCatalogItem> catalogItems)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(catalogItems);

        var materialized = entries.ToArray();
        var catalogByKey = BuildCatalogIndex(catalogItems);
        var byKey = new Dictionary<string, ArmorConversionEntry>(StringComparer.Ordinal);
        var byParamId = new Dictionary<uint, ArmorConversionEntry>();
        var variantIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in materialized)
        {
            ValidateEntry(entry, catalogByKey);
            if (!byKey.TryAdd(entry.ItemKey, entry) ||
                !byParamId.TryAdd(entry.ParamId, entry) ||
                !variantIds.Add(entry.ArmorVariantId))
            {
                throw new InvalidDataException(
                    $"Armor conversion entry duplicates a key, Param ID, or variant ID: {entry.ItemKey}");
            }
        }

        var mutableSources = materialized.ToDictionary(
            static entry => entry.ItemKey,
            static _ => new List<string>(),
            StringComparer.Ordinal);
        foreach (var entry in materialized)
        {
            var targetIds = new HashSet<uint>();
            foreach (var targetId in entry.TransformTargets)
            {
                if (!targetIds.Add(targetId) || targetId == entry.ParamId)
                {
                    throw new InvalidDataException(
                        $"Armor conversion entry has a duplicate or self target: {entry.ItemKey}");
                }

                if (!byParamId.TryGetValue(targetId, out var target) ||
                    !string.Equals(
                        entry.AlterationFamilyId,
                        target.AlterationFamilyId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Armor conversion target is missing or crosses a family: {entry.ItemKey} -> {targetId}");
                }

                mutableSources[target.ItemKey].Add(entry.ItemKey);
            }
        }

        ValidateAcquisitionKinds(materialized, mutableSources);
        var sources = mutableSources.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<string>)pair.Value
                .Order(StringComparer.Ordinal)
                .ToArray(),
            StringComparer.Ordinal);
        return new ArmorConversionGraph(materialized, byKey, sources);
    }

    public bool TryGetEntry(string itemKey, out ArmorConversionEntry? entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemKey);
        return entriesByKey.TryGetValue(itemKey, out entry);
    }

    public IReadOnlyList<string> GetDirectSources(string targetItemKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetItemKey);
        if (!sourcesByTargetKey.TryGetValue(targetItemKey, out var sources))
        {
            throw new KeyNotFoundException($"Armor conversion entry was not found: {targetItemKey}");
        }

        return sources;
    }

    private static IReadOnlyDictionary<string, ReviewedCatalogItem> BuildCatalogIndex(
        IEnumerable<ReviewedCatalogItem> catalogItems)
    {
        var result = new Dictionary<string, ReviewedCatalogItem>(StringComparer.Ordinal);
        foreach (var item in catalogItems)
        {
            if (item is null || !result.TryAdd(item.Data.Key, item))
            {
                throw new InvalidDataException("Catalog contains a null or duplicate item.");
            }
        }

        return result;
    }

    private static void ValidateEntry(
        ArmorConversionEntry entry,
        IReadOnlyDictionary<string, ReviewedCatalogItem> catalogByKey)
    {
        if (entry is null ||
            string.IsNullOrWhiteSpace(entry.ItemKey) ||
            entry.ParamId == 0 ||
            string.IsNullOrWhiteSpace(entry.ArmorVariantId) ||
            string.IsNullOrWhiteSpace(entry.AlterationFamilyId) ||
            entry.TransformTargets is null ||
            !catalogByKey.TryGetValue(entry.ItemKey, out var item) ||
            item.Data.Kind is not ItemKind.Armor ||
            item.Data.SourceKind is not ItemKind.Armor ||
            item.Data.ParamId != entry.ParamId ||
            item.Data.IsDataOnly)
        {
            throw new InvalidDataException(
                $"Armor conversion entry does not match an obtainable armor item: {entry?.ItemKey}");
        }

        var acquisitionMatchesDirectFlag = entry.AcquisitionKind switch
        {
            ArmorAcquisitionKind.DirectOnly or ArmorAcquisitionKind.DirectOrTransform =>
                entry.DirectlyObtainable,
            ArmorAcquisitionKind.TransformOnly => !entry.DirectlyObtainable,
            ArmorAcquisitionKind.Unknown => true,
            _ => false,
        };
        if (!acquisitionMatchesDirectFlag)
        {
            throw new InvalidDataException(
                $"Armor acquisition kind contradicts its direct flag: {entry.ItemKey}");
        }
    }

    private static void ValidateAcquisitionKinds(
        IEnumerable<ArmorConversionEntry> entries,
        IReadOnlyDictionary<string, List<string>> sourcesByTargetKey)
    {
        foreach (var entry in entries)
        {
            var hasTransformSource = sourcesByTargetKey[entry.ItemKey].Count > 0;
            var isValid = entry.AcquisitionKind switch
            {
                ArmorAcquisitionKind.DirectOnly => !hasTransformSource,
                ArmorAcquisitionKind.TransformOnly or ArmorAcquisitionKind.DirectOrTransform =>
                    hasTransformSource,
                ArmorAcquisitionKind.Unknown => true,
                _ => false,
            };
            if (!isValid)
            {
                throw new InvalidDataException(
                    $"Armor acquisition kind contradicts incoming conversion edges: {entry.ItemKey}");
            }
        }
    }
}
