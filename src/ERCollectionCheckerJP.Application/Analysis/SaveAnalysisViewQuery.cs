using System.Globalization;
using ERCollectionCheckerJP.Application.Completion;
using ERCollectionCheckerJP.Domain.Items;

namespace ERCollectionCheckerJP.Application.Analysis;

public enum SaveAnalysisStateFilter
{
    All,
    Missing,
    Owned,
    Unknown,
    Excluded,
}

public enum SaveAnalysisCategory
{
    All,
    Weapon,
    Armor,
    Accessory,
    AshOfWar,
    Sorcery,
    Incantation,
    Gesture,
    TorrentAttire,
    SpiritAsh,
    CrystalTear,
    OtherGoods,
}

public sealed record SaveAnalysisFilterOptions(
    CollectionMode Mode,
    IReadOnlySet<ContentPack> IncludedContentPacks,
    SaveAnalysisStateFilter State,
    SaveAnalysisCategory Category,
    string SearchText,
    bool DeveloperMode,
    bool ShowDataOnlyItems);

public sealed record SaveAnalysisProgress(
    int IncludedItemCount,
    int UnreviewedItemCount,
    int OwnedItemCount,
    int MissingItemCount,
    int UnknownItemCount,
    int CompletionDenominator,
    decimal? CompletionPercent,
    decimal? DecisionCoveragePercent);

public sealed record SaveAnalysisCategoryProgress(
    SaveAnalysisCategory Category,
    int IncludedItemCount,
    int OwnedItemCount,
    int MissingItemCount,
    int UnknownItemCount,
    decimal? CompletionPercent);

public static class SaveAnalysisViewQuery
{
    public static IReadOnlyList<SaveAnalysisItem> Filter(
        IReadOnlyList<SaveAnalysisItem> items,
        SaveAnalysisFilterOptions options)
    {
        ArgumentNullException.ThrowIfNull(items);
        Validate(options);

        return items
            .Where(item => IsVisibleInMode(item, options))
            .Where(item => options.IncludedContentPacks.Contains(item.ContentPack))
            .Where(item => options.Category is SaveAnalysisCategory.All ||
                GetCategory(item) == options.Category)
            .Where(item => MatchesState(item.State, options.State))
            .Where(item => MatchesSearch(item, options.SearchText, options.DeveloperMode))
            .ToArray();
    }

    public static SaveAnalysisProgress BuildProgress(
        IReadOnlyList<SaveAnalysisItem> items,
        CollectionMode mode,
        IReadOnlySet<ContentPack> includedContentPacks)
    {
        ArgumentNullException.ThrowIfNull(items);
        ValidateContentPacks(includedContentPacks);

        var scoped = items
            .Where(item => !item.IsDataOnly && includedContentPacks.Contains(item.ContentPack))
            .ToArray();
        var included = scoped
            .Where(item => Disposition(item, mode) is CollectionScopeDisposition.Included)
            .ToArray();
        var owned = included.Count(static item => item.State is CompletionState.Owned);
        var missing = included.Count(static item => item.State is CompletionState.Missing);
        var unknown = included.Count(static item => item.State is CompletionState.Unknown);
        var denominator = owned + missing;
        return new SaveAnalysisProgress(
            included.Length,
            scoped.Count(item =>
                Disposition(item, mode) is CollectionScopeDisposition.Unreviewed),
            owned,
            missing,
            unknown,
            denominator,
            Percentage(owned, denominator),
            Percentage(denominator, included.Length));
    }

    public static IReadOnlyList<SaveAnalysisCategoryProgress> BuildCategoryProgress(
        IReadOnlyList<SaveAnalysisItem> items,
        CollectionMode mode,
        IReadOnlySet<ContentPack> includedContentPacks)
    {
        ArgumentNullException.ThrowIfNull(items);
        ValidateContentPacks(includedContentPacks);

        return items
            .Where(item =>
                !item.IsDataOnly &&
                includedContentPacks.Contains(item.ContentPack) &&
                Disposition(item, mode) is CollectionScopeDisposition.Included)
            .GroupBy(GetCategory)
            .Where(static group => group.Key is not SaveAnalysisCategory.All)
            .Select(group =>
            {
                var owned = group.Count(static item => item.State is CompletionState.Owned);
                var missing = group.Count(static item => item.State is CompletionState.Missing);
                var unknown = group.Count(static item => item.State is CompletionState.Unknown);
                return new SaveAnalysisCategoryProgress(
                    group.Key,
                    group.Count(),
                    owned,
                    missing,
                    unknown,
                    Percentage(owned, owned + missing));
            })
            .OrderBy(static progress => progress.Category)
            .ToArray();
    }

    public static SaveAnalysisCategory GetCategory(SaveAnalysisItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind switch
        {
            ItemKind.Weapon => SaveAnalysisCategory.Weapon,
            ItemKind.Armor => SaveAnalysisCategory.Armor,
            ItemKind.Accessory => SaveAnalysisCategory.Accessory,
            ItemKind.AshOfWar => SaveAnalysisCategory.AshOfWar,
            ItemKind.Sorcery => SaveAnalysisCategory.Sorcery,
            ItemKind.Incantation => SaveAnalysisCategory.Incantation,
            ItemKind.Gesture => SaveAnalysisCategory.Gesture,
            ItemKind.TorrentAttire => SaveAnalysisCategory.TorrentAttire,
            ItemKind.Goods when item.GoodsCategory is GoodsCategory.SpiritAsh =>
                SaveAnalysisCategory.SpiritAsh,
            ItemKind.Goods when item.GoodsCategory is GoodsCategory.CrystalTear =>
                SaveAnalysisCategory.CrystalTear,
            ItemKind.Goods => SaveAnalysisCategory.OtherGoods,
            _ => SaveAnalysisCategory.OtherGoods,
        };
    }

    private static bool IsVisibleInMode(
        SaveAnalysisItem item,
        SaveAnalysisFilterOptions options)
    {
        if (item.IsDataOnly)
        {
            return options.DeveloperMode && options.ShowDataOnlyItems;
        }

        return Disposition(item, options.Mode) is CollectionScopeDisposition.Included;
    }

    private static CollectionScopeDisposition Disposition(
        SaveAnalysisItem item,
        CollectionMode mode) => mode switch
        {
            CollectionMode.Collection => item.CollectionScope,
            CollectionMode.StrictAllItems => item.StrictScope,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };

    private static bool MatchesState(CompletionState state, SaveAnalysisStateFilter filter) =>
        filter switch
        {
            SaveAnalysisStateFilter.All => true,
            SaveAnalysisStateFilter.Missing => state is CompletionState.Missing,
            SaveAnalysisStateFilter.Owned => state is CompletionState.Owned,
            SaveAnalysisStateFilter.Unknown => state is CompletionState.Unknown,
            SaveAnalysisStateFilter.Excluded => state is CompletionState.Excluded,
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null),
        };

    private static bool MatchesSearch(
        SaveAnalysisItem item,
        string searchText,
        bool developerMode)
    {
        var search = searchText?.Trim() ?? string.Empty;
        if (search.Length == 0)
        {
            return true;
        }

        if (item.NameJa?.Contains(search, StringComparison.OrdinalIgnoreCase) is true ||
            item.NameEn?.Contains(search, StringComparison.OrdinalIgnoreCase) is true)
        {
            return true;
        }

        return developerMode &&
            (item.Key.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             item.ParamId.ToString(CultureInfo.InvariantCulture).Contains(
                 search,
                 StringComparison.OrdinalIgnoreCase));
    }

    private static void Validate(SaveAnalysisFilterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateContentPacks(options.IncludedContentPacks);
    }

    private static void ValidateContentPacks(IReadOnlySet<ContentPack> contentPacks)
    {
        ArgumentNullException.ThrowIfNull(contentPacks);
        if (contentPacks.Contains(ContentPack.Unknown))
        {
            throw new ArgumentException("The visible content pack set cannot contain Unknown.");
        }
    }

    private static decimal? Percentage(int numerator, int denominator) => denominator == 0
        ? null
        : decimal.Round((decimal)numerator * 100 / denominator, 2, MidpointRounding.AwayFromZero);
}
