using ERCollectionCheckerJP.Domain.Items;
using ERCollectionCheckerJP.ItemDatabase.DataPacks;
using ERCollectionCheckerJP.ItemDatabase.Filtering;

namespace ERCollectionCheckerJP.ItemDatabase.Tests;

public sealed class ItemVisibilityPolicyTests
{
    [Fact]
    public void DataOnlyItem_IsHiddenByDefault()
    {
        var item = CreateItem(Obtainability.Unobtainable, ExclusionReason.Unobtainable);

        Assert.False(ItemVisibilityPolicy.IsVisible(item, new ItemVisibilityOptions()));
    }

    [Fact]
    public void DataOnlyItem_IsVisibleWhenExplicitlyEnabled()
    {
        var item = CreateItem(Obtainability.Unobtainable, ExclusionReason.Unobtainable);

        var visible = ItemVisibilityPolicy.IsVisible(
            item,
            new ItemVisibilityOptions(ShowDataOnlyItems: true));

        Assert.True(visible);
    }

    [Fact]
    public void DataOnlyItem_NeverCountsTowardCompletion()
    {
        var item = CreateItem(Obtainability.Unobtainable, ExclusionReason.Unobtainable);

        Assert.False(ItemVisibilityPolicy.CountsTowardCompletion(item));
    }

    [Fact]
    public void UnknownObtainability_DoesNotCountAsUnobtainable()
    {
        var item = CreateItem(Obtainability.Unknown, exclusionReason: null);

        Assert.False(item.IsDataOnly);
        Assert.False(ItemVisibilityPolicy.CountsTowardCompletion(item));
    }

    [Fact]
    public void RuntimeDataOnlyItem_RequiresExplicitVisibilityAndNeverCounts()
    {
        var item = new ReviewedRuntimeItem(
            "goods:1",
            ItemKind.Goods,
            ItemKind.Goods,
            1,
            "テスト",
            "Test",
            ContentPack.Unknown)
        {
            Obtainability = Obtainability.Unobtainable,
            ExclusionReason = ExclusionReason.Unobtainable,
        };

        Assert.False(ItemVisibilityPolicy.IsVisible(item, new ItemVisibilityOptions()));
        Assert.True(ItemVisibilityPolicy.IsVisible(
            item,
            new ItemVisibilityOptions(ShowDataOnlyItems: true)));
        Assert.False(ItemVisibilityPolicy.CountsTowardCompletion(item));
    }

    private static ItemDefinition CreateItem(
        Obtainability obtainability,
        ExclusionReason? exclusionReason) => new()
        {
            Key = "test.item",
            ParamId = 1,
            Kind = ItemKind.Goods,
            Category = "Test",
            CanonicalKey = "test.item",
            ContentPack = ContentPack.BaseGame,
            DataPresence = DataPresence.Present,
            Obtainability = obtainability,
            ExclusionReason = exclusionReason,
        };
}
