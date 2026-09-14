using ERCollectionCheckerJP.Domain.Items;
using ERCollectionCheckerJP.ItemDatabase.Catalog;

namespace ERCollectionCheckerJP.Application.Completion;

public sealed record ArmorCollectionResult(
    string ItemKey,
    ArmorCollectionState State,
    CompletionState PhysicalOwnershipState,
    string? ConversionSourceItemKey,
    IReadOnlyList<CompletionUnknownReason> UnknownReasons);

public static class ArmorCollectionEngine
{
    public static ArmorCollectionResult EvaluateItem(
        ReviewedCatalogItem item,
        IReadOnlyDictionary<string, ReviewedCatalogItem> catalogItems,
        ArmorConversionGraph conversions,
        IItemIdentityResolver identityResolver,
        CompletionObservationSnapshot observations)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(catalogItems);
        ArgumentNullException.ThrowIfNull(conversions);
        ArgumentNullException.ThrowIfNull(identityResolver);
        ArgumentNullException.ThrowIfNull(observations);

        if (item.Data.Kind is not ItemKind.Armor || item.Data.SourceKind is not ItemKind.Armor)
        {
            throw new ArgumentException("Armor collection evaluation requires an armor item.", nameof(item));
        }

        if (item.Data.IsDataOnly || item.DetectionCoverage is DetectionCoverage.Excluded)
        {
            return new ArmorCollectionResult(
                item.Data.Key,
                ArmorCollectionState.Excluded,
                CompletionState.Excluded,
                null,
                []);
        }

        var exact = EvaluateExactOwnership(item, identityResolver, observations);
        if (exact.State is CompletionState.Owned)
        {
            return new ArmorCollectionResult(
                item.Data.Key,
                ArmorCollectionState.OwnedExact,
                exact.State,
                null,
                []);
        }

        var unknownReasons = exact.RuleEvaluation.UnknownReasons.ToHashSet();
        if (!conversions.TryGetEntry(item.Data.Key, out var targetEntry) || targetEntry is null)
        {
            unknownReasons.Add(CompletionUnknownReason.UnverifiedArmorConversion);
            return Unknown(item.Data.Key, exact.State, unknownReasons);
        }

        if (!EntryMatchesItem(targetEntry, item))
        {
            unknownReasons.Add(CompletionUnknownReason.DatabaseMismatch);
            return Unknown(item.Data.Key, exact.State, unknownReasons);
        }

        var visited = new HashSet<string>(StringComparer.Ordinal) { item.Data.Key };
        var pending = new Queue<string>(conversions.GetDirectSources(item.Data.Key));
        while (pending.TryDequeue(out var sourceKey))
        {
            if (!visited.Add(sourceKey))
            {
                continue;
            }

            if (!catalogItems.TryGetValue(sourceKey, out var sourceItem) ||
                !conversions.TryGetEntry(sourceKey, out var sourceEntry) ||
                sourceEntry is null ||
                !EntryMatchesItem(sourceEntry, sourceItem))
            {
                unknownReasons.Add(CompletionUnknownReason.DatabaseMismatch);
                continue;
            }

            var source = EvaluateExactOwnership(sourceItem, identityResolver, observations);
            if (source.State is CompletionState.Owned)
            {
                return new ArmorCollectionResult(
                    item.Data.Key,
                    ArmorCollectionState.CoveredByConversion,
                    exact.State,
                    sourceKey,
                    []);
            }

            foreach (var reason in source.RuleEvaluation.UnknownReasons)
            {
                unknownReasons.Add(reason);
            }

            foreach (var ancestorKey in conversions.GetDirectSources(sourceKey))
            {
                pending.Enqueue(ancestorKey);
            }
        }

        if (targetEntry.AcquisitionKind is ArmorAcquisitionKind.Unknown)
        {
            unknownReasons.Add(CompletionUnknownReason.UnverifiedArmorConversion);
        }

        return unknownReasons.Count == 0
            ? new ArmorCollectionResult(
                item.Data.Key,
                ArmorCollectionState.Missing,
                exact.State,
                null,
                [])
            : Unknown(item.Data.Key, exact.State, unknownReasons);
    }

    private static ItemCompletionResult EvaluateExactOwnership(
        ReviewedCatalogItem item,
        IItemIdentityResolver identityResolver,
        CompletionObservationSnapshot observations)
    {
        var exactItem = new ReviewedCatalogItem(
            item.Data,
            DetectionCoverage.Reviewed,
            StandardPhysicalItemRuleFactory.CreateArmorExactOwnership(item.Data));
        return CompletionEngine.EvaluateItem(exactItem, identityResolver, observations);
    }

    private static bool EntryMatchesItem(
        ArmorConversionEntry entry,
        ReviewedCatalogItem item) =>
        string.Equals(entry.ItemKey, item.Data.Key, StringComparison.Ordinal) &&
        entry.ParamId == item.Data.ParamId &&
        item.Data.Kind is ItemKind.Armor &&
        item.Data.SourceKind is ItemKind.Armor &&
        !item.Data.IsDataOnly &&
        item.DetectionCoverage is not DetectionCoverage.Excluded;

    private static ArmorCollectionResult Unknown(
        string itemKey,
        CompletionState physicalOwnershipState,
        IEnumerable<CompletionUnknownReason> reasons) => new(
            itemKey,
            ArmorCollectionState.Unknown,
            physicalOwnershipState,
            null,
            reasons.Distinct().Order().ToArray());
}
