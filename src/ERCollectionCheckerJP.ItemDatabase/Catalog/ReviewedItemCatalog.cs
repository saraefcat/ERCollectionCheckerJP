using ERCollectionCheckerJP.Domain.Items;
using ERCollectionCheckerJP.ItemDatabase.DataPacks;

namespace ERCollectionCheckerJP.ItemDatabase.Catalog;

public enum DetectionCoverage
{
    Unsupported,
    Reviewed,
    Excluded,
}

public sealed record ReviewedCatalogItem(
    ReviewedRuntimeItem Data,
    DetectionCoverage DetectionCoverage,
    IReadOnlyList<DetectionRule> DetectionRules)
{
    public GoodsCategory? GoodsCategory { get; init; }
}

/// <summary>
/// Immutable catalog boundary. It exposes reviewed identity/localization data and the subset whose
/// Checker-owned detection rules have been reviewed, but it does not make ownership or progress decisions.
/// </summary>
public sealed class ReviewedItemCatalog : IItemIdentityResolver
{
    private readonly IReadOnlyDictionary<string, ReviewedCatalogItem> itemsByKey;
    private readonly ReviewedItemIdentityResolver identityResolver;

    internal ReviewedItemCatalog(
        IReadOnlyList<ReviewedCatalogItem> items,
        ReviewedItemIdentityResolver identityResolver,
        IReadOnlyDictionary<string, EventFlagAddressFragmentEntry> eventFlagAddresses,
        ArmorConversionGraph? armorConversions,
        GestureMappingIndex? gestureMappings)
    {
        Items = items;
        this.identityResolver = identityResolver;
        EventFlagAddresses = eventFlagAddresses;
        ArmorConversions = armorConversions;
        GestureMappings = gestureMappings;
        itemsByKey = items.ToDictionary(static item => item.Data.Key, StringComparer.Ordinal);
    }

    public IReadOnlyList<ReviewedCatalogItem> Items { get; }

    public IReadOnlyDictionary<string, EventFlagAddressFragmentEntry> EventFlagAddresses { get; }

    public ArmorConversionGraph? ArmorConversions { get; }

    public GestureMappingIndex? GestureMappings { get; }

    public bool TryGetItem(string key, out ReviewedCatalogItem? item)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return itemsByKey.TryGetValue(key, out item);
    }

    public bool TryResolveIdentity(ItemIdentity observed, out ItemIdentity canonical) =>
        identityResolver.TryResolve(observed, out canonical);
}

public static class ReviewedItemCatalogComposer
{
    public const int ExpectedTarnishedPackItemCount = 29;
    public const int ExpectedTarnishedPackAliasCount = 80;
    public const int ExpectedLegacySelfAliasCount = 8;

    public static ReviewedItemCatalog Compose(
        ReviewedItemDatabaseDataPack reviewed,
        TarnishedPackDataPack tarnishedPack,
        ArmorConversionDataPack? armorConversionPack = null,
        GestureMappingDataPack? gestureMappingPack = null,
        GoodsClassificationDataPack? goodsClassificationPack = null)
    {
        ArgumentNullException.ThrowIfNull(reviewed);
        ArgumentNullException.ThrowIfNull(tarnishedPack);

        var reviewedByIdentity = reviewed.Items.Items.ToDictionary(static item => item.Identity);
        var reviewedByKey = reviewed.Items.Items.ToDictionary(static item => item.Key, StringComparer.Ordinal);
        var japanese = tarnishedPack.Japanese.Entries.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Name,
            StringComparer.Ordinal);
        var english = tarnishedPack.English.Entries.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Name,
            StringComparer.Ordinal);
        var rules = tarnishedPack.DetectionRules.Entries.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Rules,
            StringComparer.Ordinal);

        if (tarnishedPack.Items.Items.Count != ExpectedTarnishedPackItemCount ||
            tarnishedPack.CanonicalAliases.Aliases.Count != ExpectedTarnishedPackAliasCount)
        {
            throw new InvalidDataException("Tarnished Pack overlay count is unsupported.");
        }

        var coveredKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var overlay in tarnishedPack.Items.Items)
        {
            var sourceKind = overlay.Kind is ItemKind.TorrentAttire ? ItemKind.Goods : overlay.Kind;
            var identity = new ItemIdentity(sourceKind, overlay.ParamId);
            if (!reviewedByIdentity.TryGetValue(identity, out var reviewedItem) ||
                !reviewedByKey.TryGetValue(overlay.Key, out var keyedItem) ||
                !ReferenceEquals(reviewedItem, keyedItem) ||
                reviewedItem.Kind != overlay.Kind ||
                reviewedItem.ContentPack is not ContentPack.TarnishedPack ||
                !japanese.TryGetValue(overlay.Key, out var nameJa) ||
                !english.TryGetValue(overlay.Key, out var nameEn) ||
                !string.Equals(reviewedItem.NameJa, nameJa, StringComparison.Ordinal) ||
                !string.Equals(reviewedItem.NameEn, nameEn, StringComparison.Ordinal) ||
                !rules.TryGetValue(overlay.Key, out var itemRules) ||
                itemRules.Count == 0 ||
                !coveredKeys.Add(overlay.Key))
            {
                throw new InvalidDataException($"Tarnished Pack overlay does not match reviewed item: {overlay.Key}");
            }
        }

        var reviewedTarnishedKeys = reviewed.Items.Items
            .Where(static item => item.ContentPack is ContentPack.TarnishedPack)
            .Select(static item => item.Key)
            .ToHashSet(StringComparer.Ordinal);
        if (reviewedTarnishedKeys.Count != ExpectedTarnishedPackItemCount ||
            !reviewedTarnishedKeys.SetEquals(coveredKeys))
        {
            throw new InvalidDataException("Tarnished Pack overlay does not cover the reviewed content pack exactly.");
        }

        ValidateAliases(reviewed, tarnishedPack, reviewedByKey);
        var eventFlags = ValidateEventFlags(tarnishedPack, coveredKeys);
        var armorKeys = ValidateArmorConversions(reviewed, armorConversionPack);
        var gestureKeys = ValidateGestureMappings(reviewed, gestureMappingPack);
        var goodsClassifications = ValidateGoodsClassifications(reviewed, goodsClassificationPack);

        var catalogItems = reviewed.Items.Items.Select(item =>
        {
            ReviewedCatalogItem catalogItem;
            if (item.IsDataOnly)
            {
                catalogItem = new ReviewedCatalogItem(item, DetectionCoverage.Excluded, []);
            }
            else if (rules.TryGetValue(item.Key, out var itemRules))
            {
                catalogItem = new ReviewedCatalogItem(item, DetectionCoverage.Reviewed, itemRules);
            }
            else if (armorKeys.Contains(item.Key))
            {
                catalogItem = new ReviewedCatalogItem(
                    item,
                    DetectionCoverage.Reviewed,
                    StandardPhysicalItemRuleFactory.CreateArmorExactOwnership(item));
            }
            else if (gestureKeys.Contains(item.Key))
            {
                catalogItem = new ReviewedCatalogItem(
                    item,
                    DetectionCoverage.Reviewed,
                    [new DetectionRule { Type = DetectionRuleType.GestureUnlock, ParamId = item.ParamId }]);
            }
            else
            {
                var standardRules = StandardPhysicalItemRuleFactory.Create(item);
                catalogItem = standardRules is not null
                    ? new ReviewedCatalogItem(item, DetectionCoverage.Reviewed, standardRules)
                    : new ReviewedCatalogItem(
                        item,
                        DetectionCoverage.Unsupported,
                        [new DetectionRule { Type = DetectionRuleType.Unsupported }]);
            }

            return goodsClassifications.TryGetValue(item.Key, out var classification)
                ? catalogItem with { GoodsCategory = classification.Category }
                : catalogItem;
        }).ToArray();

        var armorConversions = armorConversionPack is null
            ? null
            : ArmorConversionGraph.Create(armorConversionPack.Conversions.Entries, catalogItems);
        var gestureMappings = gestureMappingPack is null
            ? null
            : GestureMappingIndex.Create(gestureMappingPack.GestureMappings.Mappings);
        return new ReviewedItemCatalog(
            catalogItems,
            reviewed.CreateIdentityResolver(),
            eventFlags,
            armorConversions,
            gestureMappings);
    }

    private static IReadOnlyDictionary<string, GoodsClassificationEntry> ValidateGoodsClassifications(
        ReviewedItemDatabaseDataPack reviewed,
        GoodsClassificationDataPack? goodsClassificationPack)
    {
        if (goodsClassificationPack is null)
        {
            return new Dictionary<string, GoodsClassificationEntry>(StringComparer.Ordinal);
        }

        if (!string.Equals(
                goodsClassificationPack.Manifest.SourceCatalogSha256,
                reviewed.Manifest.SourceCatalogSha256,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                goodsClassificationPack.Manifest.RegulationVersion,
                reviewed.Manifest.RegulationVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Goods classification pack does not match the reviewed item database source.");
        }

        var expectedItems = reviewed.Items.Items
            .Where(static item =>
                item.Kind is ItemKind.Goods &&
                item.SourceKind is ItemKind.Goods &&
                !item.IsDataOnly)
            .ToDictionary(static item => item.Key, StringComparer.Ordinal);
        var actualItems = goodsClassificationPack.Classifications.Entries
            .ToDictionary(static entry => entry.ItemKey, StringComparer.Ordinal);
        if (actualItems.Count != expectedItems.Count ||
            !actualItems.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expectedItems.Keys) ||
            actualItems.Any(pair => pair.Value.ItemParamId != expectedItems[pair.Key].ParamId))
        {
            throw new InvalidDataException(
                "Goods classification pack does not cover obtainable Goods exactly.");
        }

        return actualItems;
    }

    private static IReadOnlySet<string> ValidateGestureMappings(
        ReviewedItemDatabaseDataPack reviewed,
        GestureMappingDataPack? gestureMappingPack)
    {
        if (gestureMappingPack is null)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        if (!string.Equals(
                gestureMappingPack.Manifest.SourceCatalogSha256,
                reviewed.Manifest.SourceCatalogSha256,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                gestureMappingPack.Manifest.RegulationVersion,
                reviewed.Manifest.RegulationVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Gesture mapping pack does not match the reviewed item database source.");
        }

        var expectedKeys = reviewed.Items.Items
            .Where(static item =>
                item.Kind is ItemKind.Gesture &&
                item.SourceKind is ItemKind.Goods &&
                !item.IsDataOnly)
            .Select(static item => item.Key)
            .ToHashSet(StringComparer.Ordinal);
        var actualKeys = gestureMappingPack.GestureMappings.Mappings
            .Select(static entry => entry.ItemKey)
            .ToHashSet(StringComparer.Ordinal);
        if (!actualKeys.SetEquals(expectedKeys))
        {
            throw new InvalidDataException(
                "Gesture mapping pack does not cover obtainable gestures exactly.");
        }

        return actualKeys;
    }

    private static IReadOnlySet<string> ValidateArmorConversions(
        ReviewedItemDatabaseDataPack reviewed,
        ArmorConversionDataPack? armorConversionPack)
    {
        if (armorConversionPack is null)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        if (!string.Equals(
                armorConversionPack.Manifest.SourceCatalogSha256,
                reviewed.Manifest.SourceCatalogSha256,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                armorConversionPack.Manifest.RegulationVersion,
                reviewed.Manifest.RegulationVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Armor conversion pack does not match the reviewed item database source.");
        }

        var expectedKeys = reviewed.Items.Items
            .Where(static item =>
                item.Kind is ItemKind.Armor &&
                item.SourceKind is ItemKind.Armor &&
                !item.IsDataOnly)
            .Select(static item => item.Key)
            .ToHashSet(StringComparer.Ordinal);
        var actualKeys = armorConversionPack.Conversions.Entries
            .Select(static entry => entry.ItemKey)
            .ToHashSet(StringComparer.Ordinal);
        if (actualKeys.Count != armorConversionPack.Conversions.Entries.Count ||
            !actualKeys.SetEquals(expectedKeys))
        {
            throw new InvalidDataException(
                "Armor conversion pack does not cover obtainable armor exactly.");
        }

        return actualKeys;
    }

    private static void ValidateAliases(
        ReviewedItemDatabaseDataPack reviewed,
        TarnishedPackDataPack tarnishedPack,
        IReadOnlyDictionary<string, ReviewedRuntimeItem> reviewedByKey)
    {
        var reviewedAliases = reviewed.Aliases.Aliases.ToHashSet();
        var selfAliasCount = 0;
        foreach (var alias in tarnishedPack.CanonicalAliases.Aliases)
        {
            if (alias.ObservedParamId <= 0 ||
                !reviewedByKey.TryGetValue(alias.CanonicalKey, out var target) ||
                target.SourceKind != alias.SourceKind)
            {
                throw new InvalidDataException(
                    $"Tarnished Pack alias target is invalid: {alias.ObservedParamId}");
            }

            if (checked((uint)alias.ObservedParamId) == target.ParamId)
            {
                selfAliasCount++;
                continue;
            }

            if (!reviewedAliases.Contains(new ReviewedRuntimeAlias(
                    alias.SourceKind,
                    checked((uint)alias.ObservedParamId),
                    target.ParamId)))
            {
                throw new InvalidDataException(
                    $"Tarnished Pack alias is absent from the reviewed database: {alias.ObservedParamId}");
            }
        }

        if (selfAliasCount != ExpectedLegacySelfAliasCount)
        {
            throw new InvalidDataException("Tarnished Pack legacy self-alias count is unsupported.");
        }
    }

    private static IReadOnlyDictionary<string, EventFlagAddressFragmentEntry> ValidateEventFlags(
        TarnishedPackDataPack tarnishedPack,
        IReadOnlySet<string> coveredKeys)
    {
        var eventFlags = tarnishedPack.EventFlagAddresses.Flags.ToDictionary(
            static entry => entry.ItemKey,
            StringComparer.Ordinal);
        foreach (var (key, address) in eventFlags)
        {
            if (!coveredKeys.Contains(key))
            {
                throw new InvalidDataException($"Event flag overlay references an unknown item: {key}");
            }

            var ruleEntry = tarnishedPack.DetectionRules.Entries.Single(entry =>
                string.Equals(entry.Key, key, StringComparison.Ordinal));
            if (ruleEntry.Rules.Count != 1 ||
                ruleEntry.Rules[0].Type is not DetectionRuleType.TorrentAttireUnlock ||
                ruleEntry.Rules[0].FlagId != address.FlagId)
            {
                throw new InvalidDataException($"Event flag overlay does not match its detection rule: {key}");
            }
        }

        return eventFlags;
    }
}
