using ERCollectionCheckerJP.Domain.Items;
using ERCollectionCheckerJP.ItemDatabase.Catalog;
using ERCollectionCheckerJP.ItemDatabase.DataPacks;

namespace ERCollectionCheckerJP.Application.Completion;

public enum CollectionScopeDisposition
{
    Included,
    Unreviewed,
    Excluded,
}

public sealed record CollectionScopeSummary(
    CollectionMode Mode,
    int CandidateItemCount,
    int IncludedItemCount,
    int UnreviewedItemCount,
    int ModeExcludedItemCount,
    decimal? DetectionCoveragePercent,
    CompletionSummary Completion);

/// <summary>
/// Defines which catalog items belong to each collection mode independently from whether their
/// ownership detection rule has been reviewed. Unreviewed classification is kept explicit rather
/// than silently treating an item as outside the selected mode.
/// </summary>
public static class CollectionScopePolicy
{
    public static CollectionScopeDisposition Classify(
        ReviewedCatalogItem item,
        CollectionMode mode)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Data.IsDataOnly)
        {
            return CollectionScopeDisposition.Excluded;
        }

        if (item.Data.Obtainability is not Obtainability.Obtainable)
        {
            return CollectionScopeDisposition.Unreviewed;
        }

        return mode switch
        {
            CollectionMode.Collection => item.Data.Kind switch
            {
                ItemKind.Weapon or
                ItemKind.Armor or
                ItemKind.Accessory or
                ItemKind.AshOfWar or
                ItemKind.Sorcery or
                ItemKind.Incantation or
                ItemKind.Gesture or
                ItemKind.TorrentAttire => CollectionScopeDisposition.Included,
                ItemKind.Goods => item.GoodsCategory is GoodsCategory category &&
                    GoodsClassificationRules.IsReviewedCollectionCategory(category)
                        ? CollectionScopeDisposition.Included
                        : CollectionScopeDisposition.Unreviewed,
                ItemKind.Unknown => CollectionScopeDisposition.Unreviewed,
                _ => CollectionScopeDisposition.Unreviewed,
            },
            CollectionMode.StrictAllItems => item.Data.Kind is ItemKind.Unknown
                ? CollectionScopeDisposition.Unreviewed
                : CollectionScopeDisposition.Included,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };
    }
}

public static class CollectionScopeSummaryBuilder
{
    public static CollectionScopeSummary Build(
        ReviewedItemCatalog catalog,
        IReadOnlyList<ItemCompletionResult> results,
        CollectionMode mode,
        IReadOnlySet<ContentPack>? includedContentPacks = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return Build(catalog.Items, results, mode, includedContentPacks);
    }

    public static CollectionScopeSummary Build(
        IReadOnlyList<ReviewedCatalogItem> items,
        IReadOnlyList<ItemCompletionResult> results,
        CollectionMode mode,
        IReadOnlySet<ContentPack>? includedContentPacks = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(results);
        if (includedContentPacks?.Contains(ContentPack.Unknown) is true)
        {
            throw new ArgumentException("Collection scope cannot include an unknown content pack.");
        }

        _ = CompletionSummaryBuilder.Build(items, results);
        var resultsByKey = results.ToDictionary(static result => result.ItemKey, StringComparer.Ordinal);
        var candidates = items
            .Where(item => includedContentPacks is null ||
                includedContentPacks.Contains(item.Data.ContentPack))
            .ToArray();
        var classified = candidates
            .Select(item => new
            {
                Item = item,
                Disposition = CollectionScopePolicy.Classify(item, mode),
            })
            .ToArray();
        var includedItems = classified
            .Where(static value => value.Disposition is CollectionScopeDisposition.Included)
            .Select(static value => value.Item)
            .ToArray();
        var includedResults = includedItems
            .Select(item => resultsByKey[item.Data.Key])
            .ToArray();
        var completion = CompletionSummaryBuilder.Build(includedItems, includedResults);
        return new CollectionScopeSummary(
            mode,
            candidates.Length,
            includedItems.Length,
            classified.Count(static value =>
                value.Disposition is CollectionScopeDisposition.Unreviewed),
            classified.Count(static value =>
                value.Disposition is CollectionScopeDisposition.Excluded),
            Percentage(completion.ReviewedRuleItemCount, includedItems.Length),
            completion);
    }

    private static decimal? Percentage(int numerator, int denominator) => denominator == 0
        ? null
        : decimal.Round((decimal)numerator * 100 / denominator, 2, MidpointRounding.AwayFromZero);
}
