using ERCollectionCheckerJP.Domain.Items;

namespace ERCollectionCheckerJP.Domain.Tests;

public sealed class ItemDefinitionTests
{
    [Fact]
    public void IsDataOnly_WhenPresentButUnobtainable_ReturnsTrue()
    {
        var item = CreateItem(Obtainability.Unobtainable);

        Assert.True(item.IsDataOnly);
    }

    [Fact]
    public void IsDataOnly_WhenObtainabilityIsUnknown_ReturnsFalse()
    {
        var item = CreateItem(Obtainability.Unknown);

        Assert.False(item.IsDataOnly);
    }

    private static ItemDefinition CreateItem(Obtainability obtainability) => new()
    {
        Key = "test.item",
        ParamId = 1,
        Kind = ItemKind.Goods,
        Category = "Test",
        CanonicalKey = "test.item",
        ContentPack = ContentPack.BaseGame,
        DataPresence = DataPresence.Present,
        Obtainability = obtainability,
    };
}
