using System.Text.Json;
using System.Text.Json.Serialization;
using ERCollectionCheckerJP.Domain.Items;
using ERCollectionCheckerJP.ItemDatabase.Catalog;
using ERCollectionCheckerJP.ItemDatabase.DataPacks;

namespace ERCollectionCheckerJP.ItemDatabase.Tests;

public sealed class ReviewedItemCatalogComposerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    [Fact]
    public async Task Compose_ReviewsTarnishedOverlayAndStandardGoods()
    {
        var tarnished = await LoadTarnishedPackAsync();
        var reviewed = CreateReviewedPack(tarnished);

        var catalog = ReviewedItemCatalogComposer.Compose(reviewed, tarnished);

        Assert.Equal(30, catalog.Items.Count);
        Assert.All(catalog.Items, static item =>
            Assert.Equal(DetectionCoverage.Reviewed, item.DetectionCoverage));
        Assert.All(
            catalog.Items.Where(static item => item.Data.ContentPack is ContentPack.TarnishedPack),
            static item => Assert.Equal(DetectionCoverage.Reviewed, item.DetectionCoverage));
        Assert.True(catalog.TryGetItem("goods:2000000", out var goods));
        Assert.NotNull(goods);
        Assert.Equal(DetectionCoverage.Reviewed, goods.DetectionCoverage);
        Assert.Collection(
            goods.DetectionRules,
            static rule => Assert.Equal(DetectionRuleType.AnyContainerItem, rule.Type));
        Assert.Equal(3, catalog.EventFlagAddresses.Count);
        Assert.Null(catalog.ArmorConversions);

        var alias = tarnished.CanonicalAliases.Aliases.First(entry =>
            checked((uint)entry.ObservedParamId) != reviewed.Items.Items.Single(item =>
                item.Key == entry.CanonicalKey).ParamId);
        var target = reviewed.Items.Items.Single(item => item.Key == alias.CanonicalKey);
        Assert.True(catalog.TryResolveIdentity(
            new ItemIdentity(alias.SourceKind, checked((uint)alias.ObservedParamId)),
            out var canonical));
        Assert.Equal(target.Identity, canonical);
    }

    [Fact]
    public async Task Compose_RejectsLocalizationMismatch()
    {
        var tarnished = await LoadTarnishedPackAsync();
        var reviewed = CreateReviewedPack(tarnished);
        var first = reviewed.Items.Items.First(static item =>
            item.ContentPack is ContentPack.TarnishedPack);
        var items = reviewed.Items.Items
            .Select(item => ReferenceEquals(item, first) ? item with { NameJa = item.NameJa + "不一致" } : item)
            .ToArray();
        reviewed = reviewed with { Items = reviewed.Items with { Items = items } };

        Assert.Throws<InvalidDataException>(() =>
            ReviewedItemCatalogComposer.Compose(reviewed, tarnished));
    }

    [Fact]
    public async Task Compose_RejectsMissingLegacyAlias()
    {
        var tarnished = await LoadTarnishedPackAsync();
        var reviewed = CreateReviewedPack(tarnished);
        var firstNonSelfAlias = tarnished.CanonicalAliases.Aliases.First(entry =>
            checked((uint)entry.ObservedParamId) != reviewed.Items.Items.Single(item =>
                item.Key == entry.CanonicalKey).ParamId);
        reviewed = reviewed with
        {
            Aliases = reviewed.Aliases with
            {
                Aliases = reviewed.Aliases.Aliases
                    .Where(alias => alias.ObservedParamId != checked((uint)firstNonSelfAlias.ObservedParamId))
                    .ToArray(),
            },
        };

        Assert.Throws<InvalidDataException>(() =>
            ReviewedItemCatalogComposer.Compose(reviewed, tarnished));
    }

    [Fact]
    public async Task Compose_MarksDataOnlyRowsAsExcluded()
    {
        var tarnished = await LoadTarnishedPackAsync();
        var reviewed = CreateReviewedPack(tarnished);
        var dataOnly = new ReviewedRuntimeItem(
            "goods:999999",
            ItemKind.Goods,
            ItemKind.Goods,
            999_999,
            "内部データ",
            "Internal data",
            ContentPack.Unknown)
        {
            Obtainability = Obtainability.Unobtainable,
            ExclusionReason = ExclusionReason.Unobtainable,
        };
        reviewed = reviewed with
        {
            Items = reviewed.Items with { Items = [.. reviewed.Items.Items, dataOnly] },
        };

        var catalog = ReviewedItemCatalogComposer.Compose(reviewed, tarnished);

        Assert.True(catalog.TryGetItem(dataOnly.Key, out var result));
        Assert.NotNull(result);
        Assert.Equal(DetectionCoverage.Excluded, result.DetectionCoverage);
        Assert.Empty(result.DetectionRules);
    }

    [Fact]
    public async Task Compose_AddsRulesOnlyForKindsWithCompleteOwnershipSources()
    {
        var tarnished = await LoadTarnishedPackAsync();
        var reviewed = CreateReviewedPack(tarnished);
        var additions = new[]
        {
            Item("weapon:910001", ItemKind.Weapon, 910_001),
            Item("accessory:910002", ItemKind.Accessory, 910_002),
            Item("ashofwar:910003", ItemKind.AshOfWar, 910_003),
            Item("armor:910004", ItemKind.Armor, 910_004),
            Item("sorcery:910005", ItemKind.Sorcery, 910_005, ItemKind.Goods),
            Item("incantation:910006", ItemKind.Incantation, 910_006, ItemKind.Goods),
            Item("goods:910008", ItemKind.Goods, 910_008),
        };
        reviewed = reviewed with
        {
            Items = reviewed.Items with { Items = [.. reviewed.Items.Items, .. additions] },
        };

        var catalog = ReviewedItemCatalogComposer.Compose(reviewed, tarnished);

        Assert.Equal(DetectionCoverage.Reviewed, Get("weapon:910001").DetectionCoverage);
        Assert.Equal(
            [DetectionRuleType.AnyContainerItem, DetectionRuleType.EquippedItem],
            ChildTypes(Get("weapon:910001")));
        Assert.Equal(DetectionCoverage.Reviewed, Get("accessory:910002").DetectionCoverage);
        Assert.Equal(
            [DetectionRuleType.AnyContainerItem, DetectionRuleType.EquippedItem],
            ChildTypes(Get("accessory:910002")));
        Assert.Equal(DetectionCoverage.Reviewed, Get("ashofwar:910003").DetectionCoverage);
        Assert.Equal(
            [
                DetectionRuleType.AnyContainerItem,
                DetectionRuleType.GaItem,
                DetectionRuleType.AttachedAshOfWar,
            ],
            ChildTypes(Get("ashofwar:910003")));
        Assert.Equal(DetectionCoverage.Unsupported, Get("armor:910004").DetectionCoverage);
        Assert.Equal(DetectionCoverage.Reviewed, Get("sorcery:910005").DetectionCoverage);
        Assert.Equal(
            [DetectionRuleType.AnyContainerItem, DetectionRuleType.EquippedSpell],
            ChildTypes(Get("sorcery:910005")));
        Assert.Equal(DetectionCoverage.Reviewed, Get("incantation:910006").DetectionCoverage);
        Assert.Equal(
            [DetectionRuleType.AnyContainerItem, DetectionRuleType.EquippedSpell],
            ChildTypes(Get("incantation:910006")));
        Assert.Equal(DetectionCoverage.Reviewed, Get("goods:910008").DetectionCoverage);
        Assert.Collection(
            Get("goods:910008").DetectionRules,
            static rule => Assert.Equal(DetectionRuleType.AnyContainerItem, rule.Type));

        ReviewedCatalogItem Get(string key) =>
            catalog.Items.Single(item => string.Equals(item.Data.Key, key, StringComparison.Ordinal));

        static DetectionRuleType[] ChildTypes(ReviewedCatalogItem item) =>
            item.DetectionRules.Single().Rules.Select(static rule => rule.Type).ToArray();
    }

    [Fact]
    public async Task Compose_WithMatchingArmorPack_ReviewsEveryObtainableArmorAndBuildsGraph()
    {
        var tarnished = await LoadTarnishedPackAsync();
        var reviewed = CreateReviewedPack(tarnished);
        var armor = Item("armor:910004", ItemKind.Armor, 910_004);
        reviewed = reviewed with
        {
            Items = reviewed.Items with { Items = [.. reviewed.Items.Items, armor] },
        };
        var armorPack = CreateArmorPack(reviewed);

        var catalog = ReviewedItemCatalogComposer.Compose(reviewed, tarnished, armorPack);

        Assert.NotNull(catalog.ArmorConversions);
        Assert.Equal(
            reviewed.Items.Items.Count(static item =>
                item.Kind is ItemKind.Armor && !item.IsDataOnly),
            catalog.ArmorConversions.Entries.Count);
        Assert.True(catalog.TryGetItem(armor.Key, out var catalogArmor));
        Assert.NotNull(catalogArmor);
        Assert.Equal(DetectionCoverage.Reviewed, catalogArmor.DetectionCoverage);
        Assert.Equal(
            [DetectionRuleType.AnyContainerItem, DetectionRuleType.EquippedItem],
            catalogArmor.DetectionRules.Single().Rules.Select(static rule => rule.Type).ToArray());
    }

    [Fact]
    public async Task Compose_RejectsArmorPackFromDifferentSourceCatalog()
    {
        var tarnished = await LoadTarnishedPackAsync();
        var reviewed = CreateReviewedPack(tarnished);
        var armorPack = CreateArmorPack(reviewed) with
        {
            Manifest = CreateArmorPack(reviewed).Manifest with
            {
                SourceCatalogSha256 = new string('D', 64),
            },
        };

        Assert.Throws<InvalidDataException>(() =>
            ReviewedItemCatalogComposer.Compose(reviewed, tarnished, armorPack));
    }

    [Fact]
    public async Task Compose_RejectsArmorPackThatDoesNotCoverObtainableArmorExactly()
    {
        var tarnished = await LoadTarnishedPackAsync();
        var reviewed = CreateReviewedPack(tarnished);
        var armorPack = CreateArmorPack(reviewed);
        armorPack = armorPack with
        {
            Conversions = armorPack.Conversions with
            {
                Entries = armorPack.Conversions.Entries.Skip(1).ToArray(),
            },
        };

        Assert.Throws<InvalidDataException>(() =>
            ReviewedItemCatalogComposer.Compose(reviewed, tarnished, armorPack));
    }

    [Fact]
    public async Task Compose_WithMatchingGesturePack_ReviewsGesturesAndBuildsMapping()
    {
        var tarnished = await LoadTarnishedPackAsync();
        var reviewed = CreateReviewedPack(tarnished);
        var gesture = Item("gesture:910007", ItemKind.Gesture, 910_007, ItemKind.Goods);
        reviewed = reviewed with
        {
            Items = reviewed.Items with { Items = [.. reviewed.Items.Items, gesture] },
        };
        var gesturePack = CreateGesturePack(reviewed, gesture);

        var catalog = ReviewedItemCatalogComposer.Compose(
            reviewed,
            tarnished,
            gestureMappingPack: gesturePack);

        Assert.NotNull(catalog.GestureMappings);
        Assert.True(catalog.GestureMappings.TryResolve(
            41,
            out var identity));
        Assert.Equal(gesture.Identity, identity);
        Assert.True(catalog.TryGetItem(gesture.Key, out var catalogGesture));
        Assert.NotNull(catalogGesture);
        Assert.Equal(DetectionCoverage.Reviewed, catalogGesture.DetectionCoverage);
        Assert.Collection(
            catalogGesture.DetectionRules,
            rule =>
            {
                Assert.Equal(DetectionRuleType.GestureUnlock, rule.Type);
                Assert.Equal(gesture.ParamId, rule.ParamId);
            });
    }

    [Fact]
    public async Task Compose_RejectsGesturePackThatDoesNotCoverObtainableGesturesExactly()
    {
        var tarnished = await LoadTarnishedPackAsync();
        var reviewed = CreateReviewedPack(tarnished);
        var gesture = Item("gesture:910007", ItemKind.Gesture, 910_007, ItemKind.Goods);
        reviewed = reviewed with
        {
            Items = reviewed.Items with { Items = [.. reviewed.Items.Items, gesture] },
        };
        var gesturePack = CreateGesturePack(reviewed, gesture) with
        {
            GestureMappings = new GestureMappingRuntimeFragment(
                1,
                "1.17",
                "reviewed-gesture-mappings",
                []),
        };

        Assert.Throws<InvalidDataException>(() => ReviewedItemCatalogComposer.Compose(
            reviewed,
            tarnished,
            gestureMappingPack: gesturePack));
    }

    [Fact]
    public async Task Compose_WithMatchingGoodsPack_ClassifiesObtainableGoods()
    {
        var tarnished = await LoadTarnishedPackAsync();
        var reviewed = CreateReviewedPack(tarnished);
        var goodsPack = CreateGoodsPack(reviewed);

        var catalog = ReviewedItemCatalogComposer.Compose(
            reviewed,
            tarnished,
            goodsClassificationPack: goodsPack);

        Assert.True(catalog.TryGetItem("goods:2000000", out var goods));
        Assert.NotNull(goods);
        Assert.Equal(GoodsCategory.SpiritAsh, goods.GoodsCategory);
        Assert.Equal(DetectionCoverage.Reviewed, goods.DetectionCoverage);
    }

    [Fact]
    public async Task Compose_RejectsGoodsPackWithoutExactCoverage()
    {
        var tarnished = await LoadTarnishedPackAsync();
        var reviewed = CreateReviewedPack(tarnished);
        var goodsPack = CreateGoodsPack(reviewed) with
        {
            Classifications = new GoodsClassificationRuntimeFragment(
                1,
                "1.17",
                "reviewed-goods-classifications",
                []),
        };

        Assert.Throws<InvalidDataException>(() => ReviewedItemCatalogComposer.Compose(
            reviewed,
            tarnished,
            goodsClassificationPack: goodsPack));
    }

    private static ReviewedItemDatabaseDataPack CreateReviewedPack(TarnishedPackDataPack tarnished)
    {
        var japanese = tarnished.Japanese.Entries.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Name,
            StringComparer.Ordinal);
        var english = tarnished.English.Entries.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Name,
            StringComparer.Ordinal);
        var items = tarnished.Items.Items.Select(item => new ReviewedRuntimeItem(
                item.Key,
                item.Kind,
                item.Kind is ItemKind.TorrentAttire ? ItemKind.Goods : item.Kind,
                item.ParamId,
                japanese[item.Key],
                english[item.Key],
                item.ContentPack))
            .Append(new ReviewedRuntimeItem(
                "goods:2000000",
                ItemKind.Goods,
                ItemKind.Goods,
                2_000_000,
                "試験用の品",
                "Test Goods",
                ContentPack.BaseGame))
            .ToArray();
        var itemByKey = items.ToDictionary(static item => item.Key, StringComparer.Ordinal);
        var aliases = tarnished.CanonicalAliases.Aliases
            .Where(alias => checked((uint)alias.ObservedParamId) != itemByKey[alias.CanonicalKey].ParamId)
            .Select(alias => new ReviewedRuntimeAlias(
                alias.SourceKind,
                checked((uint)alias.ObservedParamId),
                itemByKey[alias.CanonicalKey].ParamId))
            .ToArray();
        return new ReviewedItemDatabaseDataPack(
            new ReviewedItemDatabaseRuntimeManifest(
                2,
                "EldenRing",
                "1.17",
                "1.17",
                "test.reviewed",
                "reviewed-items-with-data-only",
                new string('A', 64),
                new string('B', 64),
                new string('C', 64),
                ["ja", "en"],
                []),
            new ReviewedRuntimeItemsFragment(2, "1.17", "reviewed-items-with-data-only", items),
            new ReviewedRuntimeAliasesFragment(2, "1.17", "reviewed-items-with-data-only", aliases));
    }

    private static ReviewedRuntimeItem Item(
        string key,
        ItemKind kind,
        uint paramId,
        ItemKind? sourceKind = null) => new(
            key,
            kind,
            sourceKind ?? kind,
            paramId,
            $"試験{paramId}",
            $"Test {paramId}",
            ContentPack.BaseGame);

    private static ArmorConversionDataPack CreateArmorPack(
        ReviewedItemDatabaseDataPack reviewed)
    {
        var entries = reviewed.Items.Items
            .Where(static item =>
                item.Kind is ItemKind.Armor &&
                item.SourceKind is ItemKind.Armor &&
                !item.IsDataOnly)
            .OrderBy(static item => item.ParamId)
            .Select(item => new ArmorConversionEntry(
                item.Key,
                item.ParamId,
                $"armor-variant:{item.ParamId}",
                ArmorVariantType.Normal,
                $"armor-family:{item.ParamId}",
                true,
                ArmorAcquisitionKind.DirectOnly,
                []))
            .ToArray();
        return new ArmorConversionDataPack(
            new ArmorConversionRuntimeManifest(
                1,
                "EldenRing",
                "1.17",
                reviewed.Manifest.RegulationVersion,
                "test.armor",
                "reviewed-armor-conversions",
                new string('A', 64),
                reviewed.Manifest.SourceCatalogSha256,
                []),
            new ArmorConversionRuntimeFragment(
                1,
                "1.17",
                "reviewed-armor-conversions",
                entries,
                []));
    }

    private static GestureMappingDataPack CreateGesturePack(
        ReviewedItemDatabaseDataPack reviewed,
        ReviewedRuntimeItem gesture) => new(
            new GestureMappingRuntimeManifest(
                1,
                "EldenRing",
                "1.17",
                reviewed.Manifest.RegulationVersion,
                "test.gestures",
                "reviewed-gesture-mappings",
                new string('D', 64),
                reviewed.Manifest.SourceCatalogSha256,
                []),
            new GestureMappingRuntimeFragment(
                1,
                "1.17",
                "reviewed-gesture-mappings",
                [new GestureMappingEntry(41, 20, gesture.Key, gesture.ParamId)]));

    private static GoodsClassificationDataPack CreateGoodsPack(
        ReviewedItemDatabaseDataPack reviewed)
    {
        var entries = reviewed.Items.Items
            .Where(static item =>
                item.Kind is ItemKind.Goods &&
                item.SourceKind is ItemKind.Goods &&
                !item.IsDataOnly)
            .OrderBy(static item => item.ParamId)
            .Select(item => new GoodsClassificationEntry(
                item.Key,
                item.ParamId,
                7,
                GoodsCategory.SpiritAsh,
                50_000,
                true))
            .ToArray();
        return new GoodsClassificationDataPack(
            new GoodsClassificationRuntimeManifest(
                1,
                "EldenRing",
                "1.17",
                reviewed.Manifest.RegulationVersion,
                "test.goods",
                "reviewed-goods-classifications",
                reviewed.Manifest.SourceCatalogSha256,
                []),
            new GoodsClassificationRuntimeFragment(
                1,
                "1.17",
                "reviewed-goods-classifications",
                entries));
    }

    private static async Task<TarnishedPackDataPack> LoadTarnishedPackAsync() => new(
        new RuntimeDataPackManifest(
            1,
            "EldenRing",
            "1.17",
            "1.17",
            "test.tarnished",
            "reviewed-fragment",
            new string('A', 64),
            ["ja", "en"],
            []),
        await ReadJsonAsync<ItemDefinitionFragment>(TarnishedPackDataPackLayout.ItemsFileName),
        await ReadJsonAsync<LocalizationFragment>(TarnishedPackDataPackLayout.JapaneseLocalizationFileName),
        await ReadJsonAsync<LocalizationFragment>(TarnishedPackDataPackLayout.EnglishLocalizationFileName),
        await ReadJsonAsync<DetectionRuleFragment>(TarnishedPackDataPackLayout.DetectionRulesFileName),
        await ReadJsonAsync<CanonicalAliasFragment>(TarnishedPackDataPackLayout.CanonicalAliasesFileName),
        await ReadJsonAsync<EventFlagAddressFragment>(TarnishedPackDataPackLayout.EventFlagAddressesFileName));

    private static async Task<T> ReadJsonAsync<T>(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "fragments", fileName);
        await using var input = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(input, JsonOptions) ??
            throw new InvalidDataException($"Test data is empty: {path}");
    }
}
