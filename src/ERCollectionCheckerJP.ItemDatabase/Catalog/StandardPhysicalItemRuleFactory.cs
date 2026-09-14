using ERCollectionCheckerJP.Domain.Items;
using ERCollectionCheckerJP.ItemDatabase.DataPacks;

namespace ERCollectionCheckerJP.ItemDatabase.Catalog;

/// <summary>
/// Creates reviewed rules only for item kinds whose complete ownership sources are already parsed.
/// Kinds that still need category curation, conversion coverage, or a dedicated save section remain
/// unsupported instead of being guessed from their Param row alone.
/// </summary>
public static class StandardPhysicalItemRuleFactory
{
    public static IReadOnlyList<DetectionRule>? Create(ReviewedRuntimeItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.IsDataOnly)
        {
            return null;
        }

        return item.Kind switch
        {
            ItemKind.Weapon or ItemKind.Accessory =>
            [
                AnyOf(
                    ItemRule(DetectionRuleType.AnyContainerItem, item.ParamId),
                    ItemRule(DetectionRuleType.EquippedItem, item.ParamId)),
            ],
            ItemKind.AshOfWar =>
            [
                AnyOf(
                    ItemRule(DetectionRuleType.AnyContainerItem, item.ParamId),
                    ItemRule(DetectionRuleType.GaItem, item.ParamId),
                    ItemRule(DetectionRuleType.AttachedAshOfWar, item.ParamId)),
            ],
            ItemKind.Sorcery or ItemKind.Incantation =>
            [
                AnyOf(
                    ItemRule(DetectionRuleType.AnyContainerItem, item.ParamId),
                    ItemRule(DetectionRuleType.EquippedSpell, item.ParamId)),
            ],
            ItemKind.Goods =>
            [
                ItemRule(DetectionRuleType.AnyContainerItem, item.ParamId),
            ],
            _ => null,
        };
    }

    /// <summary>
    /// Creates the physical-presence rule used as one input to armor collection evaluation.
    /// Armor conversion coverage is deliberately evaluated separately.
    /// </summary>
    public static IReadOnlyList<DetectionRule> CreateArmorExactOwnership(ReviewedRuntimeItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Kind is not ItemKind.Armor ||
            item.SourceKind is not ItemKind.Armor ||
            item.IsDataOnly)
        {
            throw new ArgumentException("Exact armor ownership requires an obtainable armor item.", nameof(item));
        }

        return
        [
            AnyOf(
                ItemRule(DetectionRuleType.AnyContainerItem, item.ParamId),
                ItemRule(DetectionRuleType.EquippedItem, item.ParamId)),
        ];
    }

    private static DetectionRule ItemRule(DetectionRuleType type, uint paramId) => new()
    {
        Type = type,
        ParamId = paramId,
    };

    private static DetectionRule AnyOf(params DetectionRule[] rules) => new()
    {
        Type = DetectionRuleType.AnyOf,
        Rules = rules,
    };
}
