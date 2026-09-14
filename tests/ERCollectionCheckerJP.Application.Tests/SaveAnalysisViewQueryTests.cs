using ERCollectionCheckerJP.Application.Analysis;
using ERCollectionCheckerJP.Application.Completion;
using ERCollectionCheckerJP.Domain.Items;
using ERCollectionCheckerJP.ItemDatabase.Catalog;

namespace ERCollectionCheckerJP.Application.Tests;

public sealed class SaveAnalysisViewQueryTests
{
    private static readonly IReadOnlySet<ContentPack> AllPacks = new HashSet<ContentPack>
    {
        ContentPack.BaseGame,
        ContentPack.ShadowOfTheErdtree,
        ContentPack.TarnishedPack,
    };

    [Fact]
    public void Filter_UsesModeScopeAndKeepsDataOnlyBehindDeveloperSwitch()
    {
        var items = new[]
        {
            Item("weapon:1", CompletionState.Missing),
            Item("goods:2", CompletionState.Missing) with
            {
                Kind = ItemKind.Goods,
                CollectionScope = CollectionScopeDisposition.Unreviewed,
            },
            Item("goods:3", CompletionState.Excluded) with
            {
                Kind = ItemKind.Goods,
                IsDataOnly = true,
                CollectionScope = CollectionScopeDisposition.Excluded,
                StrictScope = CollectionScopeDisposition.Excluded,
                ExclusionReason = ERCollectionCheckerJP.Domain.Items.ExclusionReason.Unobtainable,
                DetectionCoverage = DetectionCoverage.Excluded,
            },
        };

        Assert.Equal(
            ["weapon:1"],
            SaveAnalysisViewQuery.Filter(items, Options()).Select(static item => item.Key));
        Assert.Equal(
            ["weapon:1", "goods:2"],
            SaveAnalysisViewQuery.Filter(
                items,
                Options(mode: CollectionMode.StrictAllItems)).Select(static item => item.Key));
        Assert.Equal(
            ["weapon:1", "goods:3"],
            SaveAnalysisViewQuery.Filter(
                items,
                Options(
                    developerMode: true,
                    showDataOnly: true)).Select(static item => item.Key));
    }

    [Fact]
    public void Filter_SearchesBothLanguagesAndRestrictsIdentitySearchToDeveloperMode()
    {
        var item = Item("weapon:12345", CompletionState.Missing) with
        {
            NameJa = "ロングソード",
            NameEn = "Longsword",
            ParamId = 12_345,
        };

        Assert.Single(SaveAnalysisViewQuery.Filter([item], Options(search: "ロング")));
        Assert.Single(SaveAnalysisViewQuery.Filter([item], Options(search: "LONG")));
        Assert.Empty(SaveAnalysisViewQuery.Filter([item], Options(search: "12345")));
        Assert.Single(SaveAnalysisViewQuery.Filter(
            [item],
            Options(search: "12345", developerMode: true)));
    }

    [Fact]
    public void Filter_AppliesContentCategoryAndStateTogether()
    {
        var items = new[]
        {
            Item("weapon:1", CompletionState.Missing),
            Item("armor:2", CompletionState.Missing) with { Kind = ItemKind.Armor },
            Item("weapon:3", CompletionState.Owned) with
            {
                ContentPack = ContentPack.ShadowOfTheErdtree,
            },
        };
        var options = Options() with
        {
            IncludedContentPacks = new HashSet<ContentPack> { ContentPack.BaseGame },
            Category = SaveAnalysisCategory.Weapon,
            State = SaveAnalysisStateFilter.Missing,
        };

        var result = SaveAnalysisViewQuery.Filter(items, options);

        Assert.Collection(result, static item => Assert.Equal("weapon:1", item.Key));
    }

    [Fact]
    public void Progress_SeparatesUnknownFromCompletionDenominator()
    {
        var items = new[]
        {
            Item("weapon:1", CompletionState.Owned),
            Item("weapon:2", CompletionState.Missing),
            Item("weapon:3", CompletionState.Unknown),
            Item("goods:4", CompletionState.Missing) with
            {
                Kind = ItemKind.Goods,
                CollectionScope = CollectionScopeDisposition.Unreviewed,
            },
        };

        var progress = SaveAnalysisViewQuery.BuildProgress(
            items,
            CollectionMode.Collection,
            AllPacks);

        Assert.Equal(3, progress.IncludedItemCount);
        Assert.Equal(1, progress.UnreviewedItemCount);
        Assert.Equal(1, progress.OwnedItemCount);
        Assert.Equal(1, progress.MissingItemCount);
        Assert.Equal(1, progress.UnknownItemCount);
        Assert.Equal(2, progress.CompletionDenominator);
        Assert.Equal(50m, progress.CompletionPercent);
        Assert.Equal(66.67m, progress.DecisionCoveragePercent);

        var categories = SaveAnalysisViewQuery.BuildCategoryProgress(
            items,
            CollectionMode.Collection,
            AllPacks);
        var weapon = Assert.Single(categories);
        Assert.Equal(SaveAnalysisCategory.Weapon, weapon.Category);
        Assert.Equal(3, weapon.IncludedItemCount);
        Assert.Equal(50m, weapon.CompletionPercent);
    }

    private static SaveAnalysisFilterOptions Options(
        CollectionMode mode = CollectionMode.Collection,
        string search = "",
        bool developerMode = false,
        bool showDataOnly = false) => new(
            mode,
            AllPacks,
            SaveAnalysisStateFilter.All,
            SaveAnalysisCategory.All,
            search,
            developerMode,
            showDataOnly);

    private static SaveAnalysisItem Item(string key, CompletionState state) => new(
        key,
        "項目",
        "Item",
        1,
        ItemKind.Weapon,
        null,
        ContentPack.BaseGame,
        false,
        null,
        DetectionCoverage.Reviewed,
        CollectionScopeDisposition.Included,
        CollectionScopeDisposition.Included,
        state,
        [],
        null,
        null,
        null);
}
