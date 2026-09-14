using ERCollectionCheckerJP.Application.Completion;
using ERCollectionCheckerJP.Domain.Items;
using ERCollectionCheckerJP.ItemDatabase.Catalog;
using ERCollectionCheckerJP.ItemDatabase.DataPacks;

namespace ERCollectionCheckerJP.Application.Tests;

public sealed class CompletionSummaryTests
{
    [Fact]
    public void Build_SeparatesProgressCoverageAndUnsupportedItems()
    {
        var items = new[]
        {
            Item("weapon:1", ContentPack.TarnishedPack, DetectionCoverage.Reviewed),
            Item("weapon:2", ContentPack.TarnishedPack, DetectionCoverage.Reviewed),
            Item("weapon:3", ContentPack.TarnishedPack, DetectionCoverage.Reviewed),
            Item("weapon:4", ContentPack.BaseGame, DetectionCoverage.Unsupported),
        };
        var results = new[]
        {
            Result("weapon:1", CompletionState.Owned),
            Result("weapon:2", CompletionState.Missing),
            Result("weapon:3", CompletionState.Unknown),
            Result("weapon:4", CompletionState.Unknown),
        };

        var summary = CompletionSummaryBuilder.Build(items, results);

        Assert.Equal(4, summary.ScopeItemCount);
        Assert.Equal(3, summary.ReviewedRuleItemCount);
        Assert.Equal(1, summary.UnsupportedRuleItemCount);
        Assert.Equal(1, summary.OwnedItemCount);
        Assert.Equal(1, summary.MissingItemCount);
        Assert.Equal(1, summary.UnknownReviewedItemCount);
        Assert.Equal(2, summary.CompletionDenominator);
        Assert.Equal(50m, summary.CompletionPercent);
        Assert.Equal(66.67m, summary.DecisionCoveragePercent);
    }

    [Fact]
    public void Build_FiltersByContentPackWithoutChangingStates()
    {
        var items = new[]
        {
            Item("weapon:1", ContentPack.TarnishedPack, DetectionCoverage.Reviewed),
            Item("weapon:2", ContentPack.BaseGame, DetectionCoverage.Unsupported),
        };
        var results = new[]
        {
            Result("weapon:1", CompletionState.Owned),
            Result("weapon:2", CompletionState.Unknown),
        };

        var summary = CompletionSummaryBuilder.Build(
            items,
            results,
            new HashSet<ContentPack> { ContentPack.TarnishedPack });

        Assert.Equal(1, summary.ScopeItemCount);
        Assert.Equal(1, summary.ReviewedRuleItemCount);
        Assert.Equal(0, summary.UnsupportedRuleItemCount);
        Assert.Equal(100m, summary.CompletionPercent);
        Assert.Equal(100m, summary.DecisionCoveragePercent);
    }

    [Fact]
    public void Build_UsesNullPercentagesWhenNothingIsDecidable()
    {
        var items = new[]
        {
            Item("weapon:1", ContentPack.BaseGame, DetectionCoverage.Unsupported),
        };
        var results = new[]
        {
            Result("weapon:1", CompletionState.Unknown),
        };

        var summary = CompletionSummaryBuilder.Build(items, results);

        Assert.Null(summary.CompletionPercent);
        Assert.Null(summary.DecisionCoveragePercent);
    }

    [Fact]
    public void Build_RejectsResultSetThatDoesNotMatchCatalog()
    {
        var items = new[]
        {
            Item("weapon:1", ContentPack.BaseGame, DetectionCoverage.Reviewed),
        };

        Assert.Throws<InvalidDataException>(() => CompletionSummaryBuilder.Build(
            items,
            [Result("weapon:2", CompletionState.Unknown)]));
    }

    [Fact]
    public void Build_RejectsDefinitiveStateForUnsupportedItem()
    {
        var items = new[]
        {
            Item("weapon:1", ContentPack.BaseGame, DetectionCoverage.Unsupported),
        };

        Assert.Throws<InvalidDataException>(() => CompletionSummaryBuilder.Build(
            items,
            [Result("weapon:1", CompletionState.Owned)]));
    }

    [Fact]
    public void Build_ReportsDataOnlyItemsOutsideProgressAndCoverage()
    {
        var items = new[]
        {
            Item("weapon:1", ContentPack.BaseGame, DetectionCoverage.Reviewed),
            Item("weapon:2", ContentPack.Unknown, DetectionCoverage.Excluded),
        };
        var results = new[]
        {
            Result("weapon:1", CompletionState.Owned),
            Result("weapon:2", CompletionState.Excluded),
        };

        var summary = CompletionSummaryBuilder.Build(items, results);

        Assert.Equal(2, summary.ScopeItemCount);
        Assert.Equal(1, summary.ReviewedRuleItemCount);
        Assert.Equal(0, summary.UnsupportedRuleItemCount);
        Assert.Equal(1, summary.ExcludedItemCount);
        Assert.Equal(1, summary.CompletionDenominator);
        Assert.Equal(100m, summary.CompletionPercent);
        Assert.Equal(100m, summary.DecisionCoveragePercent);
    }

    [Fact]
    public void CollectionScope_SeparatesIncludedUnreviewedAndDataOnlyItems()
    {
        var items = new[]
        {
            Item("weapon:1", ItemKind.Weapon, ContentPack.BaseGame, DetectionCoverage.Reviewed),
            Item("sorcery:2", ItemKind.Sorcery, ContentPack.BaseGame, DetectionCoverage.Unsupported),
            Item("goods:3", ItemKind.Goods, ContentPack.BaseGame, DetectionCoverage.Unsupported),
            Item("goods:4", ItemKind.Goods, ContentPack.Unknown, DetectionCoverage.Excluded),
        };
        var results = new[]
        {
            Result("weapon:1", CompletionState.Owned),
            Result("sorcery:2", CompletionState.Unknown),
            Result("goods:3", CompletionState.Unknown),
            Result("goods:4", CompletionState.Excluded),
        };

        var summary = CollectionScopeSummaryBuilder.Build(
            items,
            results,
            CollectionMode.Collection);

        Assert.Equal(4, summary.CandidateItemCount);
        Assert.Equal(2, summary.IncludedItemCount);
        Assert.Equal(1, summary.UnreviewedItemCount);
        Assert.Equal(1, summary.ModeExcludedItemCount);
        Assert.Equal(50m, summary.DetectionCoveragePercent);
        Assert.Equal(1, summary.Completion.ReviewedRuleItemCount);
        Assert.Equal(1, summary.Completion.UnsupportedRuleItemCount);
    }

    [Fact]
    public void StrictScope_IncludesObtainableGoodsWithoutMakingUnsupportedRulesDefinitive()
    {
        var items = new[]
        {
            Item("weapon:1", ItemKind.Weapon, ContentPack.BaseGame, DetectionCoverage.Reviewed),
            Item("goods:2", ItemKind.Goods, ContentPack.BaseGame, DetectionCoverage.Unsupported),
        };
        var results = new[]
        {
            Result("weapon:1", CompletionState.Missing),
            Result("goods:2", CompletionState.Unknown),
        };

        var summary = CollectionScopeSummaryBuilder.Build(
            items,
            results,
            CollectionMode.StrictAllItems);

        Assert.Equal(2, summary.IncludedItemCount);
        Assert.Equal(0, summary.UnreviewedItemCount);
        Assert.Equal(50m, summary.DetectionCoveragePercent);
        Assert.Equal(1, summary.Completion.MissingItemCount);
        Assert.Equal(1, summary.Completion.UnsupportedRuleItemCount);
    }

    [Fact]
    public void CollectionScope_ContentPackFilterDoesNotReclassifyItems()
    {
        var items = new[]
        {
            Item("weapon:1", ItemKind.Weapon, ContentPack.BaseGame, DetectionCoverage.Reviewed),
            Item("weapon:2", ItemKind.Weapon, ContentPack.TarnishedPack, DetectionCoverage.Reviewed),
        };
        var results = new[]
        {
            Result("weapon:1", CompletionState.Missing),
            Result("weapon:2", CompletionState.Owned),
        };

        var summary = CollectionScopeSummaryBuilder.Build(
            items,
            results,
            CollectionMode.Collection,
            new HashSet<ContentPack> { ContentPack.TarnishedPack });

        Assert.Equal(1, summary.CandidateItemCount);
        Assert.Equal(1, summary.IncludedItemCount);
        Assert.Equal(1, summary.Completion.OwnedItemCount);
        Assert.Equal(100m, summary.DetectionCoveragePercent);
    }

    [Theory]
    [InlineData(ItemKind.Sorcery, CollectionScopeDisposition.Included)]
    [InlineData(ItemKind.Incantation, CollectionScopeDisposition.Included)]
    [InlineData(ItemKind.Gesture, CollectionScopeDisposition.Included)]
    [InlineData(ItemKind.Goods, CollectionScopeDisposition.Unreviewed)]
    [InlineData(ItemKind.Unknown, CollectionScopeDisposition.Unreviewed)]
    public void CollectionScope_FailsClosedForKindsWithoutCollectionClassification(
        ItemKind kind,
        CollectionScopeDisposition expected)
    {
        var item = Item("item:1", kind, ContentPack.BaseGame, DetectionCoverage.Unsupported);

        Assert.Equal(expected, CollectionScopePolicy.Classify(item, CollectionMode.Collection));
    }

    [Fact]
    public void CollectionScope_IncludesOnlyReviewedPermanentGoodsCategories()
    {
        var spiritAsh = Item(
            "goods:1",
            ItemKind.Goods,
            ContentPack.BaseGame,
            DetectionCoverage.Reviewed) with
        {
            GoodsCategory = GoodsCategory.SpiritAsh,
        };
        var crystalTear = Item(
            "goods:2",
            ItemKind.Goods,
            ContentPack.BaseGame,
            DetectionCoverage.Reviewed) with
        {
            GoodsCategory = GoodsCategory.CrystalTear,
        };
        var consumedKeyItem = Item(
            "goods:3",
            ItemKind.Goods,
            ContentPack.BaseGame,
            DetectionCoverage.Reviewed) with
        {
            GoodsCategory = GoodsCategory.KeyItem,
        };

        Assert.Equal(
            CollectionScopeDisposition.Included,
            CollectionScopePolicy.Classify(spiritAsh, CollectionMode.Collection));
        Assert.Equal(
            CollectionScopeDisposition.Included,
            CollectionScopePolicy.Classify(crystalTear, CollectionMode.Collection));
        Assert.Equal(
            CollectionScopeDisposition.Unreviewed,
            CollectionScopePolicy.Classify(consumedKeyItem, CollectionMode.Collection));
    }

    private static ReviewedCatalogItem Item(
        string key,
        ContentPack contentPack,
        DetectionCoverage coverage) => Item(key, ItemKind.Weapon, contentPack, coverage);

    private static ReviewedCatalogItem Item(
        string key,
        ItemKind kind,
        ContentPack contentPack,
        DetectionCoverage coverage)
    {
        var data = new ReviewedRuntimeItem(
                key,
                kind,
                kind is ItemKind.Sorcery or ItemKind.Incantation or ItemKind.Gesture
                    ? ItemKind.Goods
                    : kind,
                uint.Parse(key[(key.IndexOf(':') + 1)..]),
                key,
                key,
                contentPack);
        if (coverage is DetectionCoverage.Excluded)
        {
            data = data with
            {
                Obtainability = Obtainability.Unobtainable,
                ExclusionReason = ExclusionReason.Unobtainable,
            };
        }

        return new ReviewedCatalogItem(
            data,
            coverage,
            coverage is DetectionCoverage.Reviewed
                ? [new DetectionRule { Type = DetectionRuleType.InventoryItem, ParamId = 1 }]
                : coverage is DetectionCoverage.Unsupported
                    ? [new DetectionRule { Type = DetectionRuleType.Unsupported }]
                    : []);
    }

    private static ItemCompletionResult Result(string key, CompletionState state) => new(
        key,
        state,
        new RuleEvaluationResult(
            state switch
            {
                CompletionState.Owned => RuleMatchState.Matched,
                CompletionState.Missing => RuleMatchState.NotMatched,
                CompletionState.Excluded => RuleMatchState.NotMatched,
                _ => RuleMatchState.Unknown,
            },
            state is CompletionState.Unknown ? [CompletionUnknownReason.UnsupportedRule] : []));
}
