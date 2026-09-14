using ERCollectionCheckerJP.Domain.Items;
using ERCollectionCheckerJP.ItemDatabase.Catalog;
using ERCollectionCheckerJP.ItemDatabase.DataPacks;

namespace ERCollectionCheckerJP.ItemDatabase.Tests;

public sealed class ArmorConversionGraphTests
{
    [Fact]
    public void Create_PreservesDirectedEdgeWithoutInferringReverse()
    {
        var source = Item(100);
        var target = Item(200);
        var graph = ArmorConversionGraph.Create(
            [
                Entry(source, "family-a", ArmorAcquisitionKind.DirectOnly, true, target.Data.ParamId),
                Entry(target, "family-a", ArmorAcquisitionKind.TransformOnly, false),
            ],
            [source, target]);

        Assert.Empty(graph.GetDirectSources(source.Data.Key));
        Assert.Equal([source.Data.Key], graph.GetDirectSources(target.Data.Key));
    }

    [Fact]
    public void Create_AllowsExplicitEdgesInBothDirections()
    {
        var normal = Item(100);
        var altered = Item(200);
        var graph = ArmorConversionGraph.Create(
            [
                Entry(normal, "family-a", ArmorAcquisitionKind.DirectOrTransform, true, altered.Data.ParamId),
                Entry(altered, "family-a", ArmorAcquisitionKind.DirectOrTransform, true, normal.Data.ParamId),
            ],
            [normal, altered]);

        Assert.Equal([altered.Data.Key], graph.GetDirectSources(normal.Data.Key));
        Assert.Equal([normal.Data.Key], graph.GetDirectSources(altered.Data.Key));
    }

    [Fact]
    public void Create_AllowsIndependentDirectArmorWithoutConversionEdges()
    {
        var armor = Item(100);

        var graph = ArmorConversionGraph.Create(
            [Entry(armor, "independent-family", ArmorAcquisitionKind.DirectOnly, true)],
            [armor]);

        Assert.Empty(graph.GetDirectSources(armor.Data.Key));
    }

    [Fact]
    public void Create_RejectsDanglingTarget()
    {
        var armor = Item(100);

        Assert.Throws<InvalidDataException>(() => ArmorConversionGraph.Create(
            [Entry(armor, "family-a", ArmorAcquisitionKind.DirectOnly, true, 999)],
            [armor]));
    }

    [Fact]
    public void Create_RejectsCrossFamilyEdge()
    {
        var source = Item(100);
        var target = Item(200);

        Assert.Throws<InvalidDataException>(() => ArmorConversionGraph.Create(
            [
                Entry(source, "family-a", ArmorAcquisitionKind.DirectOnly, true, target.Data.ParamId),
                Entry(target, "family-b", ArmorAcquisitionKind.TransformOnly, false),
            ],
            [source, target]));
    }

    [Fact]
    public void Create_RejectsDuplicateAndSelfTargets()
    {
        var source = Item(100);
        var target = Item(200);

        Assert.Throws<InvalidDataException>(() => ArmorConversionGraph.Create(
            [
                Entry(
                    source,
                    "family-a",
                    ArmorAcquisitionKind.DirectOnly,
                    true,
                    target.Data.ParamId,
                    target.Data.ParamId),
                Entry(target, "family-a", ArmorAcquisitionKind.TransformOnly, false),
            ],
            [source, target]));
        Assert.Throws<InvalidDataException>(() => ArmorConversionGraph.Create(
            [Entry(source, "family-a", ArmorAcquisitionKind.Unknown, true, source.Data.ParamId)],
            [source]));
    }

    [Fact]
    public void Create_RejectsAcquisitionKindThatContradictsIncomingEdge()
    {
        var source = Item(100);
        var target = Item(200);

        Assert.Throws<InvalidDataException>(() => ArmorConversionGraph.Create(
            [
                Entry(source, "family-a", ArmorAcquisitionKind.DirectOnly, true, target.Data.ParamId),
                Entry(target, "family-a", ArmorAcquisitionKind.DirectOnly, true),
            ],
            [source, target]));
    }

    [Fact]
    public void Create_RejectsCatalogMismatchAndDataOnlyArmor()
    {
        var armor = Item(100);
        var wrongParam = Entry(armor, "family-a", ArmorAcquisitionKind.DirectOnly, true) with
        {
            ParamId = 101,
        };
        var dataOnly = armor with
        {
            Data = armor.Data with
            {
                Obtainability = Obtainability.Unobtainable,
                ExclusionReason = ExclusionReason.Unobtainable,
            },
            DetectionCoverage = DetectionCoverage.Excluded,
        };

        Assert.Throws<InvalidDataException>(() => ArmorConversionGraph.Create([wrongParam], [armor]));
        Assert.Throws<InvalidDataException>(() => ArmorConversionGraph.Create(
            [Entry(dataOnly, "family-a", ArmorAcquisitionKind.DirectOnly, true)],
            [dataOnly]));
    }

    private static ArmorConversionEntry Entry(
        ReviewedCatalogItem item,
        string family,
        ArmorAcquisitionKind acquisitionKind,
        bool directlyObtainable,
        params uint[] targets) => new(
            item.Data.Key,
            item.Data.ParamId,
            $"variant-{item.Data.ParamId}",
            ArmorVariantType.Normal,
            family,
            directlyObtainable,
            acquisitionKind,
            targets);

    private static ReviewedCatalogItem Item(uint paramId) => new(
        new ReviewedRuntimeItem(
            $"armor:{paramId}",
            ItemKind.Armor,
            ItemKind.Armor,
            paramId,
            $"防具{paramId}",
            $"Armor {paramId}",
            ContentPack.BaseGame),
        DetectionCoverage.Unsupported,
        [new DetectionRule { Type = DetectionRuleType.Unsupported }]);
}
