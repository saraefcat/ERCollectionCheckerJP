using ERCollectionCheckerJP.Application.Completion;
using ERCollectionCheckerJP.Domain.Items;
using ERCollectionCheckerJP.ItemDatabase.Catalog;
using ERCollectionCheckerJP.ItemDatabase.DataPacks;
using ERCollectionCheckerJP.SaveParser.Flags;
using ERCollectionCheckerJP.SaveParser.Items;

namespace ERCollectionCheckerJP.Application.Tests;

public sealed class CompletionEngineTests
{
    private static readonly ItemIdentity Longsword = new(ItemKind.Weapon, 2_000_000);
    private static readonly ItemIdentity LongswordAffinity = new(ItemKind.Weapon, 2_000_100);

    [Fact]
    public void AnyContainer_NormalizesAliasBeforeOwnedDecision()
    {
        var result = CompletionEngine.EvaluateItem(
            Item(DetectionRuleType.AnyContainerItem),
            Resolver(),
            Snapshot(inventory: Complete(LongswordAffinity)));

        Assert.Equal(CompletionState.Owned, result.State);
        Assert.Equal(RuleMatchState.Matched, result.RuleEvaluation.State);
        Assert.Empty(result.RuleEvaluation.UnknownReasons);
    }

    [Fact]
    public void AnyContainer_ReturnsMissingOnlyWhenBothSourcesAreComplete()
    {
        var result = CompletionEngine.EvaluateItem(
            Item(DetectionRuleType.AnyContainerItem),
            Resolver(),
            Snapshot(inventory: Complete(), storage: Complete()));

        Assert.Equal(CompletionState.Missing, result.State);
        Assert.Equal(RuleMatchState.NotMatched, result.RuleEvaluation.State);
    }

    [Fact]
    public void AnyContainer_ReturnsUnknownWhenOneSourceWasNotParsed()
    {
        var result = CompletionEngine.EvaluateItem(
            Item(DetectionRuleType.AnyContainerItem),
            Resolver(),
            Snapshot(inventory: Complete()));

        Assert.Equal(CompletionState.Unknown, result.State);
        Assert.Equal([CompletionUnknownReason.SourceNotParsed], result.RuleEvaluation.UnknownReasons);
    }

    [Fact]
    public void EquippedSpell_MatchesGoodsBackedSpellIdentity()
    {
        const uint spellId = 6_050;
        var identity = new ItemIdentity(ItemKind.Goods, spellId);
        var item = new ReviewedCatalogItem(
            new ReviewedRuntimeItem(
                "sorcery:6050",
                ItemKind.Sorcery,
                ItemKind.Goods,
                spellId,
                "魔術",
                "Sorcery",
                ContentPack.BaseGame),
            DetectionCoverage.Reviewed,
            [new DetectionRule { Type = DetectionRuleType.EquippedSpell, ParamId = spellId }]);
        var observations = Snapshot() with
        {
            EquippedSpells = Complete(identity),
        };

        var result = CompletionEngine.EvaluateItem(
            item,
            new StubIdentityResolver(new Dictionary<ItemIdentity, ItemIdentity>
            {
                [identity] = identity,
            }),
            observations);

        Assert.Equal(CompletionState.Owned, result.State);
    }

    [Fact]
    public void UnknownSameKindIdentity_PreventsMissingDecision()
    {
        var result = CompletionEngine.EvaluateItem(
            Item(DetectionRuleType.AnyContainerItem),
            Resolver(),
            Snapshot(
                inventory: Complete(new ItemIdentity(ItemKind.Weapon, 777_777_777)),
                storage: Complete()));

        Assert.Equal(CompletionState.Unknown, result.State);
        Assert.Equal([CompletionUnknownReason.UnknownItemId], result.RuleEvaluation.UnknownReasons);
    }

    [Fact]
    public void UnknownDifferentKindIdentity_DoesNotTaintCompleteSource()
    {
        var result = CompletionEngine.EvaluateItem(
            Item(DetectionRuleType.AnyContainerItem),
            Resolver(),
            Snapshot(
                inventory: Complete(new ItemIdentity(ItemKind.Armor, 777_777_777)),
                storage: Complete()));

        Assert.Equal(CompletionState.Missing, result.State);
    }

    [Fact]
    public void UnsupportedCoverage_RemainsUnknownEvenWhenObserved()
    {
        var item = Item(DetectionRuleType.AnyContainerItem) with
        {
            DetectionCoverage = DetectionCoverage.Unsupported,
        };

        var result = CompletionEngine.EvaluateItem(
            item,
            Resolver(),
            Snapshot(inventory: Complete(Longsword)));

        Assert.Equal(CompletionState.Unknown, result.State);
        Assert.Equal([CompletionUnknownReason.UnsupportedRule], result.RuleEvaluation.UnknownReasons);
    }

    [Fact]
    public void DataOnlyCoverage_IsExcludedWithoutInspectingSaveObservations()
    {
        var dataOnly = Item(DetectionRuleType.AnyContainerItem) with
        {
            Data = Item(DetectionRuleType.AnyContainerItem).Data with
            {
                Obtainability = Obtainability.Unobtainable,
                ExclusionReason = ExclusionReason.Unobtainable,
            },
            DetectionCoverage = DetectionCoverage.Excluded,
            DetectionRules = [],
        };

        var result = CompletionEngine.EvaluateItem(
            dataOnly,
            Resolver(),
            Snapshot(inventory: Complete(Longsword)));

        Assert.Equal(CompletionState.Excluded, result.State);
        Assert.Equal(RuleMatchState.NotMatched, result.RuleEvaluation.State);
        Assert.Empty(result.RuleEvaluation.UnknownReasons);
    }

    [Theory]
    [InlineData(ObservationSourceStatus.Complete, true, CompletionState.Owned)]
    [InlineData(ObservationSourceStatus.Complete, false, CompletionState.Missing)]
    [InlineData(ObservationSourceStatus.NotParsed, false, CompletionState.Unknown)]
    [InlineData(ObservationSourceStatus.Corrupt, false, CompletionState.Unknown)]
    public void EventFlag_UsesThreeStateSourceCompleteness(
        ObservationSourceStatus status,
        bool isSet,
        CompletionState expected)
    {
        var source = status switch
        {
            ObservationSourceStatus.Complete => ObservationSource<uint>.Complete(isSet ? [60101u] : []),
            ObservationSourceStatus.NotParsed => ObservationSource<uint>.NotParsed(),
            ObservationSourceStatus.Corrupt => ObservationSource<uint>.Corrupt(),
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };
        var data = new ReviewedRuntimeItem(
            "torrentattire:2009600",
            ItemKind.TorrentAttire,
            ItemKind.Goods,
            2_009_600,
            "霊馬装束",
            "Torrent Attire",
            ContentPack.TarnishedPack);
        var item = new ReviewedCatalogItem(
            data,
            DetectionCoverage.Reviewed,
            [new DetectionRule { Type = DetectionRuleType.TorrentAttireUnlock, FlagId = 60101 }]);
        var result = CompletionEngine.EvaluateItem(
            item,
            new StubIdentityResolver(new Dictionary<ItemIdentity, ItemIdentity>
            {
                [data.Identity] = data.Identity,
            }),
            Snapshot(eventFlags: source));

        Assert.Equal(expected, result.State);
    }

    [Fact]
    public void AllOf_NotMatchedDominatesUnknown()
    {
        var item = Item(DetectionRuleType.AnyContainerItem) with
        {
            DetectionRules =
            [
                new DetectionRule
                {
                    Type = DetectionRuleType.AllOf,
                    Rules =
                    [
                        new DetectionRule
                        {
                            Type = DetectionRuleType.InventoryItem,
                            ParamId = Longsword.ParamId,
                        },
                        new DetectionRule
                        {
                            Type = DetectionRuleType.StorageItem,
                            ParamId = Longsword.ParamId,
                        },
                    ],
                },
            ],
        };

        var result = CompletionEngine.EvaluateItem(
            item,
            Resolver(),
            Snapshot(inventory: Complete()));

        Assert.Equal(CompletionState.Missing, result.State);
        Assert.Equal(RuleMatchState.NotMatched, result.RuleEvaluation.State);
    }

    [Fact]
    public void InvalidRuleData_FailsClosedAsUnknown()
    {
        var item = Item(DetectionRuleType.InventoryItem) with
        {
            DetectionRules = [new DetectionRule { Type = DetectionRuleType.InventoryItem }],
        };

        var result = CompletionEngine.EvaluateItem(item, Resolver(), Snapshot(inventory: Complete()));

        Assert.Equal(CompletionState.Unknown, result.State);
        Assert.Equal([CompletionUnknownReason.DatabaseMismatch], result.RuleEvaluation.UnknownReasons);
    }

    [Fact]
    public void ObservationBuilder_PreservesResolvedItemsUnresolvedKindsAndReviewedFlags()
    {
        var inventory = Inventory(
            Record(ItemKind.Weapon, LongswordAffinity.ParamId, quantity: 1),
            Record(ItemKind.Armor, null, quantity: 1),
            Record(ItemKind.Weapon, null, quantity: 0));
        var slot = new byte[] { 0, 0b0000_0100, 0, 0 };

        var observations = CompletionObservationBuilder.FromParsedSections(
            slot,
            new GaItemSection(0, 0, [], new Dictionary<uint, uint>()),
            inventory,
            Storage(),
            new EventFlagsSection(0, slot.Length, 0, 0),
            [
                new EventFlagAddressFragmentEntry("torrentattire:1", 60101, 1, 2),
                new EventFlagAddressFragmentEntry("torrentattire:2", 60102, 2, 0),
            ]);

        Assert.Equal(ObservationSourceStatus.Complete, observations.Inventory.Status);
        Assert.Contains(LongswordAffinity, observations.Inventory.Values);
        Assert.Equal([ItemKind.Armor], observations.Inventory.UnresolvedSourceKinds);
        Assert.Equal(ObservationSourceStatus.Complete, observations.Storage.Status);
        Assert.Equal([60101u], observations.EventFlags.Values);
        Assert.Equal(ObservationSourceStatus.NotParsed, observations.EquippedItems.Status);
        Assert.Equal(ObservationSourceStatus.Complete, observations.AttachedAshesOfWar.Status);
        Assert.Equal(ObservationSourceStatus.NotParsed, observations.Gestures.Status);
    }

    [Fact]
    public void ObservationBuilder_UsesValidatedEquipmentAndNormalizesWeaponUpgrade()
    {
        var equipment = new EquipmentSection(
            0,
            0,
            0,
            [
                new EquippedItemRecord(
                    EquippedItemSlot.RightHandArmament1,
                    1,
                    0,
                    0,
                    LongswordAffinity.ParamId + 7,
                    0x8000_0001,
                    ItemKind.Weapon,
                    LongswordAffinity.ParamId + 7,
                    LongswordAffinity.ParamId + 7,
                    InventoryItemResolution.GaItemMap),
            ]);
        var observations = CompletionObservationBuilder.FromParsedSections(
            [],
            new GaItemSection(0, 0, [], new Dictionary<uint, uint>()),
            Inventory(),
            equipment,
            Storage(),
            new EventFlagsSection(0, 0, 0, 0),
            []);

        var result = CompletionEngine.EvaluateItem(
            Item(DetectionRuleType.EquippedItem),
            Resolver(),
            observations);

        Assert.Equal(ObservationSourceStatus.Complete, observations.EquippedItems.Status);
        Assert.Contains(LongswordAffinity, observations.EquippedItems.Values);
        Assert.Equal(CompletionState.Owned, result.State);
    }

    [Fact]
    public void ObservationBuilder_UsesValidatedEquippedSpells()
    {
        const uint spellId = 6_050;
        var equippedSpells = new EquippedSpellSection(
            0,
            EquippedSpellSectionReader.SectionSize,
            0,
            [new EquippedSpellRecord(0, 0, spellId, uint.MaxValue)]);
        var observations = CompletionObservationBuilder.FromParsedSections(
            [],
            new GaItemSection(0, 0, [], new Dictionary<uint, uint>()),
            Inventory(),
            new EquipmentSection(0, 0, 0, []),
            equippedSpells,
            Storage(),
            new EventFlagsSection(0, 0, 0, 0),
            []);

        Assert.Equal(ObservationSourceStatus.Complete, observations.EquippedSpells.Status);
        Assert.Equal(
            [new ItemIdentity(ItemKind.Goods, spellId)],
            observations.EquippedSpells.Values);
    }

    [Fact]
    public void ObservationBuilder_MapsPersistentGesturesAndPreservesUnknownIds()
    {
        const uint itemParamId = 9_011;
        var gestureSection = new GestureSection(
            0,
            GestureSectionReader.SectionSize,
            [new GestureRecord(0, 0, 41), new GestureRecord(1, 4, 999)]);
        var gestureMappings = GestureMappingIndex.Create(
            [new GestureMappingEntry(41, 20, "gesture:9011", itemParamId)]);
        var observations = CompletionObservationBuilder.FromParsedSections(
            [],
            new GaItemSection(0, 0, [], new Dictionary<uint, uint>()),
            Inventory(),
            new EquipmentSection(0, 0, 0, []),
            new EquippedSpellSection(0, 0, -1, []),
            Storage(),
            gestureSection,
            gestureMappings,
            new EventFlagsSection(0, 0, 0, 0),
            []);

        Assert.Equal(ObservationSourceStatus.Complete, observations.Gestures.Status);
        Assert.Equal(
            [new ItemIdentity(ItemKind.Goods, itemParamId)],
            observations.Gestures.Values);
        Assert.Empty(observations.Gestures.UnresolvedSourceKinds);

        var gestureItem = new ReviewedCatalogItem(
            new ReviewedRuntimeItem(
                "gesture:9011",
                ItemKind.Gesture,
                ItemKind.Goods,
                itemParamId,
                "前を指す",
                "Point Forwards",
                ContentPack.BaseGame),
            DetectionCoverage.Reviewed,
            [new DetectionRule { Type = DetectionRuleType.GestureUnlock, ParamId = itemParamId }]);
        var result = CompletionEngine.EvaluateItem(
            gestureItem,
            new StubIdentityResolver(new Dictionary<ItemIdentity, ItemIdentity>
            {
                [gestureItem.Data.Identity] = gestureItem.Data.Identity,
            }),
            observations);

        Assert.Equal(CompletionState.Owned, result.State);
    }

    [Fact]
    public void ObservationBuilder_UnresolvedKindPreventsFalseMissing()
    {
        var observations = CompletionObservationBuilder.FromParsedSections(
            [],
            new GaItemSection(0, 0, [], new Dictionary<uint, uint>()),
            Inventory(Record(ItemKind.Weapon, null, quantity: 1)),
            Storage(),
            new EventFlagsSection(0, 0, 0, 0),
            []);

        var result = CompletionEngine.EvaluateItem(
            Item(DetectionRuleType.AnyContainerItem),
            Resolver(),
            observations);

        Assert.Equal(CompletionState.Unknown, result.State);
        Assert.Equal([CompletionUnknownReason.UnknownItemId], result.RuleEvaluation.UnknownReasons);
    }

    [Fact]
    public void ObservationBuilder_RemovesWeaponReinforcementSuffixBeforeAliasResolution()
    {
        var observations = CompletionObservationBuilder.FromParsedSections(
            [],
            new GaItemSection(0, 0, [], new Dictionary<uint, uint>()),
            Inventory(Record(ItemKind.Weapon, LongswordAffinity.ParamId + 7, quantity: 1)),
            Storage(),
            new EventFlagsSection(0, 0, 0, 0),
            []);

        var result = CompletionEngine.EvaluateItem(
            Item(DetectionRuleType.AnyContainerItem),
            Resolver(),
            observations);

        Assert.Equal(CompletionState.Owned, result.State);
    }

    [Fact]
    public void ObservationBuilder_ResolvesAttachedAshOfWarHandle()
    {
        const uint ashHandle = 0xC000_0123;
        const uint ashRawItemId = 0x8000_5654;
        var gaItems = new GaItemSection(
            0,
            0,
            [
                new GaItemRecord(
                    0,
                    0,
                    0x8000_0001,
                    Longsword.ParamId,
                    ItemKind.Weapon,
                    Longsword.ParamId,
                    null,
                    null,
                    ashHandle,
                    null),
                new GaItemRecord(
                    1,
                    0,
                    ashHandle,
                    ashRawItemId,
                    ItemKind.AshOfWar,
                    22_100,
                    null,
                    null,
                    null,
                    null),
            ],
            new Dictionary<uint, uint>
            {
                [0x8000_0001] = Longsword.ParamId,
                [ashHandle] = ashRawItemId,
            });
        var observations = CompletionObservationBuilder.FromParsedSections(
            [],
            gaItems,
            Inventory(),
            Storage(),
            new EventFlagsSection(0, 0, 0, 0),
            []);
        var ashIdentity = new ItemIdentity(ItemKind.AshOfWar, 22_100);
        var ashItem = new ReviewedCatalogItem(
            new ReviewedRuntimeItem(
                "ashofwar:22100",
                ItemKind.AshOfWar,
                ItemKind.AshOfWar,
                ashIdentity.ParamId,
                "戦灰",
                "Ash of War",
                ContentPack.BaseGame),
            DetectionCoverage.Reviewed,
            [
                new DetectionRule
                {
                    Type = DetectionRuleType.AttachedAshOfWar,
                    ParamId = ashIdentity.ParamId,
                },
            ]);

        var result = CompletionEngine.EvaluateItem(
            ashItem,
            new StubIdentityResolver(new Dictionary<ItemIdentity, ItemIdentity>
            {
                [ashIdentity] = ashIdentity,
            }),
            observations);

        Assert.Equal(CompletionState.Owned, result.State);
        Assert.Contains(ashIdentity, observations.AttachedAshesOfWar.Values);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ObservationBuilder_UnresolvedOrWrongKindAttachedAshHandlePreventsMissing(
        bool pointsToWrongKind)
    {
        var handleMap = new Dictionary<uint, uint>
        {
            [0x8000_0001] = Longsword.ParamId,
        };
        if (pointsToWrongKind)
        {
            handleMap[0xC000_0123] = 0x4000_1234;
        }

        var gaItems = new GaItemSection(
            0,
            0,
            [
                new GaItemRecord(
                    0,
                    0,
                    0x8000_0001,
                    Longsword.ParamId,
                    ItemKind.Weapon,
                    Longsword.ParamId,
                    null,
                    null,
                    0xC000_0123,
                    null),
            ],
            handleMap);
        var observations = CompletionObservationBuilder.FromParsedSections(
            [],
            gaItems,
            Inventory(),
            Storage(),
            new EventFlagsSection(0, 0, 0, 0),
            []);
        var ashIdentity = new ItemIdentity(ItemKind.AshOfWar, 22_100);
        var ashItem = new ReviewedCatalogItem(
            new ReviewedRuntimeItem(
                "ashofwar:22100",
                ItemKind.AshOfWar,
                ItemKind.AshOfWar,
                ashIdentity.ParamId,
                "戦灰",
                "Ash of War",
                ContentPack.BaseGame),
            DetectionCoverage.Reviewed,
            [
                new DetectionRule
                {
                    Type = DetectionRuleType.AttachedAshOfWar,
                    ParamId = ashIdentity.ParamId,
                },
            ]);

        var result = CompletionEngine.EvaluateItem(
            ashItem,
            new StubIdentityResolver(new Dictionary<ItemIdentity, ItemIdentity>
            {
                [ashIdentity] = ashIdentity,
            }),
            observations);

        Assert.Equal(CompletionState.Unknown, result.State);
        Assert.Equal([CompletionUnknownReason.UnknownItemId], result.RuleEvaluation.UnknownReasons);
        Assert.Equal([ItemKind.AshOfWar], observations.AttachedAshesOfWar.UnresolvedSourceKinds);
    }

    private static ReviewedCatalogItem Item(DetectionRuleType type) => new(
        new ReviewedRuntimeItem(
            "weapon:2000000",
            ItemKind.Weapon,
            ItemKind.Weapon,
            Longsword.ParamId,
            "ロングソード",
            "Longsword",
            ContentPack.BaseGame),
        DetectionCoverage.Reviewed,
        [new DetectionRule { Type = type, ParamId = Longsword.ParamId }]);

    private static StubIdentityResolver Resolver() => new(new Dictionary<ItemIdentity, ItemIdentity>
    {
        [Longsword] = Longsword,
        [LongswordAffinity] = Longsword,
    });

    private static CompletionObservationSnapshot Snapshot(
        ItemObservationSource? inventory = null,
        ItemObservationSource? storage = null,
        ObservationSource<uint>? eventFlags = null) => new(
            inventory ?? ItemObservationSource.NotParsed(),
            storage ?? ItemObservationSource.NotParsed(),
            ItemObservationSource.NotParsed(),
            ItemObservationSource.NotParsed(),
            ItemObservationSource.NotParsed(),
            ItemObservationSource.NotParsed(),
            eventFlags ?? ObservationSource<uint>.NotParsed());

    private static ItemObservationSource Complete(params ItemIdentity[] values) =>
        ItemObservationSource.Complete(values);

    private static InventoryItemRecord Record(ItemKind kind, uint? paramId, uint quantity) => new(
        InventoryContainer.Common,
        0,
        0,
        1,
        quantity,
        0,
        kind,
        paramId,
        paramId,
        paramId is null ? InventoryItemResolution.Unresolved : InventoryItemResolution.GaItemMap);

    private static InventorySection Inventory(params InventoryItemRecord[] records) => new(
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        records);

    private static StorageSection Storage(params InventoryItemRecord[] records) => new(
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        records);

    private sealed class StubIdentityResolver(
        IReadOnlyDictionary<ItemIdentity, ItemIdentity> identities) : IItemIdentityResolver
    {
        public bool TryResolveIdentity(ItemIdentity observed, out ItemIdentity canonical) =>
            identities.TryGetValue(observed, out canonical);
    }
}
