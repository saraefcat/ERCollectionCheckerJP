using ERCollectionCheckerJP.Domain.Items;
using ERCollectionCheckerJP.ItemDatabase.Catalog;

namespace ERCollectionCheckerJP.Application.Completion;

public sealed record CompletionSummary(
    int ScopeItemCount,
    int ReviewedRuleItemCount,
    int UnsupportedRuleItemCount,
    int OwnedItemCount,
    int MissingItemCount,
    int UnknownReviewedItemCount,
    int ExcludedItemCount,
    int CompletionDenominator,
    decimal? CompletionPercent,
    decimal? DecisionCoveragePercent);

public static class CompletionSummaryBuilder
{
    public static CompletionSummary Build(
        ReviewedItemCatalog catalog,
        IReadOnlyList<ItemCompletionResult> results,
        IReadOnlySet<ContentPack>? includedContentPacks = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return Build(catalog.Items, results, includedContentPacks);
    }

    public static CompletionSummary Build(
        IReadOnlyList<ReviewedCatalogItem> items,
        IReadOnlyList<ItemCompletionResult> results,
        IReadOnlySet<ContentPack>? includedContentPacks = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(results);
        if (includedContentPacks?.Contains(ContentPack.Unknown) is true)
        {
            throw new ArgumentException("Completion scope cannot include an unknown content pack.");
        }

        IReadOnlyDictionary<string, ItemCompletionResult> resultsByKey;
        try
        {
            resultsByKey = results.ToDictionary(static result => result.ItemKey, StringComparer.Ordinal);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("Completion results contain a duplicate item key.", exception);
        }

        var itemKeys = items.Select(static item => item.Data.Key).ToHashSet(StringComparer.Ordinal);
        if (itemKeys.Count != items.Count ||
            resultsByKey.Count != results.Count ||
            !itemKeys.SetEquals(resultsByKey.Keys))
        {
            throw new InvalidDataException("Completion results do not match the catalog snapshot exactly.");
        }

        var scopedItems = items
            .Where(item => includedContentPacks is null ||
                includedContentPacks.Contains(item.Data.ContentPack))
            .ToArray();
        var reviewed = 0;
        var unsupported = 0;
        var owned = 0;
        var missing = 0;
        var unknown = 0;
        var excluded = 0;
        foreach (var item in scopedItems)
        {
            var result = resultsByKey[item.Data.Key];
            if (item.DetectionCoverage is DetectionCoverage.Excluded)
            {
                excluded++;
                if (!item.Data.IsDataOnly || result.State is not CompletionState.Excluded)
                {
                    throw new InvalidDataException(
                        $"Excluded item has an invalid completion state: {item.Data.Key}");
                }

                continue;
            }

            if (item.DetectionCoverage is DetectionCoverage.Unsupported)
            {
                unsupported++;
                if (result.State is not CompletionState.Unknown)
                {
                    throw new InvalidDataException(
                        $"Unsupported item has a definitive completion state: {item.Data.Key}");
                }

                continue;
            }

            reviewed++;
            switch (result.State)
            {
                case CompletionState.Owned:
                    owned++;
                    break;
                case CompletionState.Missing:
                    missing++;
                    break;
                case CompletionState.Unknown:
                    unknown++;
                    break;
                case CompletionState.Excluded:
                    throw new InvalidDataException(
                        $"Collectible item was excluded from completion: {item.Data.Key}");
                default:
                    throw new InvalidDataException(
                        $"Item has an unsupported completion state: {item.Data.Key}");
            }
        }

        var denominator = owned + missing;
        return new CompletionSummary(
            scopedItems.Length,
            reviewed,
            unsupported,
            owned,
            missing,
            unknown,
            excluded,
            denominator,
            Percentage(owned, denominator),
            Percentage(denominator, reviewed));
    }

    private static decimal? Percentage(int numerator, int denominator) => denominator == 0
        ? null
        : decimal.Round((decimal)numerator * 100 / denominator, 2, MidpointRounding.AwayFromZero);
}
