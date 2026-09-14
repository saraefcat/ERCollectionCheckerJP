using ERCollectionCheckerJP.Domain.Items;
using ERCollectionCheckerJP.ItemDatabase.DataPacks;

namespace ERCollectionCheckerJP.ItemDatabase.Filtering;

public sealed record ItemVisibilityOptions(
    bool ShowExcludedItems = false,
    bool ShowDataOnlyItems = false);

public static class ItemVisibilityPolicy
{
    public static bool IsVisible(ItemDefinition item, ItemVisibilityOptions options)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(options);

        if (item.IsDataOnly)
        {
            return options.ShowDataOnlyItems;
        }

        return item.ExclusionReason is null || options.ShowExcludedItems;
    }

    public static bool CountsTowardCompletion(ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item.DataPresence is DataPresence.Present &&
               item.Obtainability is Obtainability.Obtainable &&
               item.ExclusionReason is null;
    }

    public static bool IsVisible(ReviewedRuntimeItem item, ItemVisibilityOptions options)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(options);

        if (item.IsDataOnly)
        {
            return options.ShowDataOnlyItems;
        }

        return item.ExclusionReason is null || options.ShowExcludedItems;
    }

    public static bool CountsTowardCompletion(ReviewedRuntimeItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item.DataPresence is DataPresence.Present &&
               item.Obtainability is Obtainability.Obtainable &&
               item.ExclusionReason is null;
    }
}
