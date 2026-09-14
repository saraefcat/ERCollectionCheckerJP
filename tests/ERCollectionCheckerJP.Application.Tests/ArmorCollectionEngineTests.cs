using ERCollectionCheckerJP.Application.Completion;
using ERCollectionCheckerJP.Domain.Items;
using ERCollectionCheckerJP.ItemDatabase.Catalog;
using ERCollectionCheckerJP.ItemDatabase.DataPacks;

namespace ERCollectionCheckerJP.Application.Tests;

public sealed class ArmorCollectionEngineTests
{
    [Fact]
    public void ExactOwnership_DominatesMissingConversionReview()
    {
        var armor = Item(100);

        var result = Evaluate(
            armor,
            [armor],
            [],
            Snapshot(inventory: Complete(armor.Data.Identity)));

        Assert.Equal(ArmorCollectionState.OwnedExact, result.State);
        Assert.Equal(CompletionState.Owned, result.PhysicalOwnershipState);
        Assert.Null(result.ConversionSourceItemKey);
    }

    [Fact]
    public void DirectedSourceOwnership_CoversConversionTarget()
    {
        var source = Item(100);
        var target = Item(200);

        var result = Evaluate(
            target,
            [source, target],
            [
                Entry(source, ArmorAcquisitionKind.DirectOnly, true, target.Data.ParamId),
                Entry(target, ArmorAcquisitionKind.TransformOnly, false),
            ],
            CompleteSnapshot(source.Data.Identity));

        Assert.Equal(ArmorCollectionState.CoveredByConversion, result.State);
        Assert.Equal(CompletionState.Missing, result.PhysicalOwnershipState);
        Assert.Equal(source.Data.Key, result.ConversionSourceItemKey);
    }

    [Fact]
    public void CompletionProjection_PreservesConversionEvidenceWhileCountingTargetAsOwned()
    {
        var source = Item(100);
        var target = Item(200);
        var items = new[] { source, target };
        var byKey = items.ToDictionary(static item => item.Data.Key, StringComparer.Ordinal);
        var graph = ArmorConversionGraph.Create(
            [
                Entry(source, ArmorAcquisitionKind.DirectOnly, true, target.Data.ParamId),
                Entry(target, ArmorAcquisitionKind.TransformOnly, false),
            ],
            items);

        var result = CompletionEngine.EvaluateArmorItem(
            target,
            byKey,
            graph,
            new StubIdentityResolver(items.ToDictionary(static item => item.Data.Identity)),
            CompleteSnapshot(source.Data.Identity));

        Assert.Equal(CompletionState.Owned, result.State);
        Assert.Equal(RuleMatchState.Matched, result.RuleEvaluation.State);
        Assert.NotNull(result.ArmorCollection);
        Assert.Equal(ArmorCollectionState.CoveredByConversion, result.ArmorCollection.State);
        Assert.Equal(CompletionState.Missing, result.ArmorCollection.PhysicalOwnershipState);
        Assert.Equal(source.Data.Key, result.ArmorCollection.ConversionSourceItemKey);
    }

    [Fact]
    public void ReverseConversion_IsNotInferred()
    {
        var source = Item(100);
        var target = Item(200);

        var result = Evaluate(
            source,
            [source, target],
            [
                Entry(source, ArmorAcquisitionKind.DirectOnly, true, target.Data.ParamId),
                Entry(target, ArmorAcquisitionKind.TransformOnly, false),
            ],
            CompleteSnapshot(target.Data.Identity));

        Assert.Equal(ArmorCollectionState.Missing, result.State);
        Assert.Null(result.ConversionSourceItemKey);
    }

    [Theory]
    [InlineData(200100u, 201100u)]
    [InlineData(360100u, 361100u)]
    [InlineData(370100u, 371100u)]
    [InlineData(800100u, 801100u)]
    public void DirectlyObtainableAlteredException_IsNotCoveredByNormal(
        uint normalParamId,
        uint alteredParamId)
    {
        var normal = Item(normalParamId);
        var altered = Item(alteredParamId);

        var result = Evaluate(
            altered,
            [normal, altered],
            [
                Entry(normal, ArmorAcquisitionKind.DirectOnly, true),
                Entry(altered, ArmorAcquisitionKind.DirectOnly, true),
            ],
            CompleteSnapshot(normal.Data.Identity));

        Assert.Equal(ArmorCollectionState.Missing, result.State);
        Assert.Equal(CompletionState.Missing, result.PhysicalOwnershipState);
        Assert.Null(result.ConversionSourceItemKey);
    }

    [Fact]
    public void ExplicitReverseConversion_CoversTarget()
    {
        var normal = Item(100);
        var altered = Item(200);

        var result = Evaluate(
            normal,
            [normal, altered],
            [
                Entry(normal, ArmorAcquisitionKind.DirectOrTransform, true, altered.Data.ParamId),
                Entry(altered, ArmorAcquisitionKind.DirectOrTransform, true, normal.Data.ParamId),
            ],
            CompleteSnapshot(altered.Data.Identity));

        Assert.Equal(ArmorCollectionState.CoveredByConversion, result.State);
        Assert.Equal(altered.Data.Key, result.ConversionSourceItemKey);
    }

    [Fact]
    public void TransitiveDirectedConversion_CoversTarget()
    {
        var source = Item(100);
        var middle = Item(200);
        var target = Item(300);

        var result = Evaluate(
            target,
            [source, middle, target],
            [
                Entry(source, ArmorAcquisitionKind.DirectOnly, true, middle.Data.ParamId),
                Entry(middle, ArmorAcquisitionKind.TransformOnly, false, target.Data.ParamId),
                Entry(target, ArmorAcquisitionKind.TransformOnly, false),
            ],
            CompleteSnapshot(source.Data.Identity));

        Assert.Equal(ArmorCollectionState.CoveredByConversion, result.State);
        Assert.Equal(source.Data.Key, result.ConversionSourceItemKey);
    }

    [Fact]
    public void KnownSourceCanCoverTargetWhoseExactOwnershipIsUnknown()
    {
        var source = Item(100);
        var target = Item(200);

        var result = Evaluate(
            target,
            [source, target],
            [
                Entry(source, ArmorAcquisitionKind.DirectOnly, true, target.Data.ParamId),
                Entry(target, ArmorAcquisitionKind.TransformOnly, false),
            ],
            Snapshot(
                inventory: Complete(source.Data.Identity),
                equipment: Complete()));

        Assert.Equal(ArmorCollectionState.CoveredByConversion, result.State);
        Assert.Equal(CompletionState.Unknown, result.PhysicalOwnershipState);
        Assert.Equal(source.Data.Key, result.ConversionSourceItemKey);
    }

    [Fact]
    public void MissingGraphEntry_FailsClosedAsUnknown()
    {
        var armor = Item(100);

        var result = Evaluate(armor, [armor], [], CompleteSnapshot());

        Assert.Equal(ArmorCollectionState.Unknown, result.State);
        Assert.Equal(CompletionState.Missing, result.PhysicalOwnershipState);
        Assert.Equal(
            [CompletionUnknownReason.UnverifiedArmorConversion],
            result.UnknownReasons);
    }

    [Fact]
    public void RuntimeCatalogMismatch_FailsClosedAsUnknown()
    {
        var source = Item(100);
        var target = Item(200);
        var items = new[] { source, target };
        var graph = ArmorConversionGraph.Create(
            [
                Entry(source, ArmorAcquisitionKind.DirectOnly, true, target.Data.ParamId),
                Entry(target, ArmorAcquisitionKind.TransformOnly, false),
            ],
            items);
        var mismatchedSource = source with
        {
            Data = source.Data with { ParamId = 999 },
        };
        var catalogItems = new Dictionary<string, ReviewedCatalogItem>(StringComparer.Ordinal)
        {
            [source.Data.Key] = mismatchedSource,
            [target.Data.Key] = target,
        };
        var resolver = new StubIdentityResolver(items.ToDictionary(static item => item.Data.Identity));

        var result = ArmorCollectionEngine.EvaluateItem(
            target,
            catalogItems,
            graph,
            resolver,
            CompleteSnapshot(source.Data.Identity));

        Assert.Equal(ArmorCollectionState.Unknown, result.State);
        Assert.Equal([CompletionUnknownReason.DatabaseMismatch], result.UnknownReasons);
    }

    [Fact]
    public void UnknownAcquisitionKind_PreventsMissingDecision()
    {
        var armor = Item(100);

        var result = Evaluate(
            armor,
            [armor],
            [Entry(armor, ArmorAcquisitionKind.Unknown, false)],
            CompleteSnapshot());

        Assert.Equal(ArmorCollectionState.Unknown, result.State);
        Assert.Equal(
            [CompletionUnknownReason.UnverifiedArmorConversion],
            result.UnknownReasons);
    }

    [Fact]
    public void IncompletePhysicalSource_PreventsMissingDecision()
    {
        var armor = Item(100);

        var result = Evaluate(
            armor,
            [armor],
            [Entry(armor, ArmorAcquisitionKind.DirectOnly, true)],
            Snapshot(inventory: Complete(), equipment: Complete()));

        Assert.Equal(ArmorCollectionState.Unknown, result.State);
        Assert.Equal(CompletionState.Unknown, result.PhysicalOwnershipState);
        Assert.Equal([CompletionUnknownReason.SourceNotParsed], result.UnknownReasons);
    }

    [Fact]
    public void DataOnlyArmor_IsExcludedBeforeConversionReview()
    {
        var armor = Item(100);
        armor = armor with
        {
            Data = armor.Data with
            {
                Obtainability = Obtainability.Unobtainable,
                ExclusionReason = ExclusionReason.Unobtainable,
            },
            DetectionCoverage = DetectionCoverage.Excluded,
            DetectionRules = [],
        };

        var result = Evaluate(armor, [armor], [], CompletionObservationSnapshot.Empty);

        Assert.Equal(ArmorCollectionState.Excluded, result.State);
        Assert.Equal(CompletionState.Excluded, result.PhysicalOwnershipState);
        Assert.Empty(result.UnknownReasons);
    }

    private static ArmorCollectionResult Evaluate(
        ReviewedCatalogItem target,
        IReadOnlyList<ReviewedCatalogItem> items,
        IReadOnlyList<ArmorConversionEntry> entries,
        CompletionObservationSnapshot snapshot)
    {
        var byKey = items.ToDictionary(static item => item.Data.Key, StringComparer.Ordinal);
        var graph = ArmorConversionGraph.Create(entries, items);
        var identities = items.ToDictionary(static item => item.Data.Identity);
        return ArmorCollectionEngine.EvaluateItem(
            target,
            byKey,
            graph,
            new StubIdentityResolver(identities),
            snapshot);
    }

    private static ArmorConversionEntry Entry(
        ReviewedCatalogItem item,
        ArmorAcquisitionKind acquisitionKind,
        bool directlyObtainable,
        params uint[] targets) => new(
            item.Data.Key,
            item.Data.ParamId,
            $"variant-{item.Data.ParamId}",
            ArmorVariantType.Normal,
            "family-a",
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

    private static CompletionObservationSnapshot CompleteSnapshot(
        params ItemIdentity[] identities) => Snapshot(
            inventory: Complete(identities),
            storage: Complete(),
            equipment: Complete());

    private static CompletionObservationSnapshot Snapshot(
        ItemObservationSource? inventory = null,
        ItemObservationSource? storage = null,
        ItemObservationSource? equipment = null) => new(
            inventory ?? ItemObservationSource.NotParsed(),
            storage ?? ItemObservationSource.NotParsed(),
            ItemObservationSource.NotParsed(),
            equipment ?? ItemObservationSource.NotParsed(),
            ItemObservationSource.NotParsed(),
            ItemObservationSource.NotParsed(),
            ObservationSource<uint>.NotParsed());

    private static ItemObservationSource Complete(params ItemIdentity[] values) =>
        ItemObservationSource.Complete(values);

    private sealed class StubIdentityResolver(
        IReadOnlyDictionary<ItemIdentity, ReviewedCatalogItem> items) : IItemIdentityResolver
    {
        public bool TryResolveIdentity(ItemIdentity observed, out ItemIdentity canonical)
        {
            canonical = observed;
            return items.ContainsKey(observed);
        }
    }
}
