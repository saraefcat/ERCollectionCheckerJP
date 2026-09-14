using ERCollectionCheckerJP.Domain.Items;
using ERCollectionCheckerJP.ItemDatabase.Catalog;
using ERCollectionCheckerJP.ItemDatabase.DataPacks;

namespace ERCollectionCheckerJP.Application.Completion;

public enum RuleMatchState
{
    Matched,
    NotMatched,
    Unknown,
}

public enum CompletionUnknownReason
{
    UnsupportedRule,
    SourceNotParsed,
    SourceCorrupt,
    UnknownItemId,
    DatabaseMismatch,
    UnverifiedArmorConversion,
}

public sealed record RuleEvaluationResult(
    RuleMatchState State,
    IReadOnlyList<CompletionUnknownReason> UnknownReasons);

public sealed record ItemCompletionResult(
    string ItemKey,
    CompletionState State,
    RuleEvaluationResult RuleEvaluation)
{
    public ArmorCollectionResult? ArmorCollection { get; init; }
}

public static class CompletionEngine
{
    private const int MaximumRuleDepth = 64;

    public static IReadOnlyList<ItemCompletionResult> EvaluateCatalog(
        ReviewedItemCatalog catalog,
        CompletionObservationSnapshot observations)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(observations);
        var itemsByKey = catalog.Items.ToDictionary(
            static item => item.Data.Key,
            StringComparer.Ordinal);
        return catalog.Items
            .Select(item => EvaluateCatalogItem(item, itemsByKey, catalog, observations))
            .ToArray();
    }

    private static ItemCompletionResult EvaluateCatalogItem(
        ReviewedCatalogItem item,
        IReadOnlyDictionary<string, ReviewedCatalogItem> itemsByKey,
        ReviewedItemCatalog catalog,
        CompletionObservationSnapshot observations)
    {
        if (item.Data.Kind is not ItemKind.Armor || catalog.ArmorConversions is null)
        {
            return EvaluateItem(item, catalog, observations);
        }

        return EvaluateArmorItem(
            item,
            itemsByKey,
            catalog.ArmorConversions,
            catalog,
            observations);
    }

    public static ItemCompletionResult EvaluateArmorItem(
        ReviewedCatalogItem item,
        IReadOnlyDictionary<string, ReviewedCatalogItem> catalogItems,
        ArmorConversionGraph conversions,
        IItemIdentityResolver identityResolver,
        CompletionObservationSnapshot observations)
    {
        var armor = ArmorCollectionEngine.EvaluateItem(
            item,
            catalogItems,
            conversions,
            identityResolver,
            observations);
        var result = armor.State switch
        {
            ArmorCollectionState.OwnedExact or ArmorCollectionState.CoveredByConversion =>
                new ItemCompletionResult(item.Data.Key, CompletionState.Owned, Matched()),
            ArmorCollectionState.Missing =>
                new ItemCompletionResult(item.Data.Key, CompletionState.Missing, NotMatched()),
            ArmorCollectionState.Unknown => new ItemCompletionResult(
                item.Data.Key,
                CompletionState.Unknown,
                new RuleEvaluationResult(RuleMatchState.Unknown, armor.UnknownReasons)),
            ArmorCollectionState.Excluded =>
                new ItemCompletionResult(item.Data.Key, CompletionState.Excluded, NotMatched()),
            _ => throw new ArgumentOutOfRangeException(nameof(armor)),
        };
        return result with { ArmorCollection = armor };
    }

    public static ItemCompletionResult EvaluateItem(
        ReviewedCatalogItem item,
        IItemIdentityResolver identityResolver,
        CompletionObservationSnapshot observations)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(identityResolver);
        ArgumentNullException.ThrowIfNull(observations);

        RuleEvaluationResult evaluation;
        if (item.DetectionCoverage is DetectionCoverage.Excluded)
        {
            return new ItemCompletionResult(
                item.Data.Key,
                CompletionState.Excluded,
                NotMatched());
        }

        if (item.DetectionCoverage is DetectionCoverage.Unsupported)
        {
            evaluation = Unknown(CompletionUnknownReason.UnsupportedRule);
        }
        else if (item.DetectionRules.Count == 0)
        {
            evaluation = Unknown(CompletionUnknownReason.UnsupportedRule);
        }
        else
        {
            evaluation = EvaluateAnyOf(
                item.DetectionRules.Select(rule => EvaluateRule(
                    rule,
                    item.Data,
                    identityResolver,
                    observations,
                    0)));
        }

        var state = evaluation.State switch
        {
            RuleMatchState.Matched => CompletionState.Owned,
            RuleMatchState.NotMatched => CompletionState.Missing,
            RuleMatchState.Unknown => CompletionState.Unknown,
            _ => throw new ArgumentOutOfRangeException(nameof(evaluation)),
        };
        return new ItemCompletionResult(item.Data.Key, state, evaluation);
    }

    private static RuleEvaluationResult EvaluateRule(
        DetectionRule rule,
        ReviewedRuntimeItem item,
        IItemIdentityResolver identityResolver,
        CompletionObservationSnapshot observations,
        int depth)
    {
        if (rule is null || depth >= MaximumRuleDepth)
        {
            return Unknown(CompletionUnknownReason.DatabaseMismatch);
        }

        return rule.Type switch
        {
            DetectionRuleType.InventoryItem => EvaluateIdentitySource(
                rule,
                item,
                identityResolver,
                observations.Inventory),
            DetectionRuleType.StorageItem => EvaluateIdentitySource(
                rule,
                item,
                identityResolver,
                observations.Storage),
            DetectionRuleType.AnyContainerItem => EvaluateAnyOf(
                [
                    EvaluateIdentitySource(rule, item, identityResolver, observations.Inventory),
                    EvaluateIdentitySource(rule, item, identityResolver, observations.Storage),
                ]),
            DetectionRuleType.GaItem => EvaluateIdentitySource(
                rule,
                item,
                identityResolver,
                observations.GaItems),
            DetectionRuleType.EquippedItem => EvaluateIdentitySource(
                rule,
                item,
                identityResolver,
                observations.EquippedItems),
            DetectionRuleType.EquippedSpell => EvaluateIdentitySource(
                rule,
                item,
                identityResolver,
                observations.EquippedSpells),
            DetectionRuleType.AttachedAshOfWar => EvaluateIdentitySource(
                rule,
                item,
                identityResolver,
                observations.AttachedAshesOfWar),
            DetectionRuleType.GestureUnlock => EvaluateIdentitySource(
                rule,
                item,
                identityResolver,
                observations.Gestures),
            DetectionRuleType.EventFlag or DetectionRuleType.TorrentAttireUnlock =>
                EvaluateEventFlag(rule, observations.EventFlags),
            DetectionRuleType.AnyOf => rule.Rules.Count == 0
                ? Unknown(CompletionUnknownReason.DatabaseMismatch)
                : EvaluateAnyOf(rule.Rules.Select(child => EvaluateRule(
                    child,
                    item,
                    identityResolver,
                    observations,
                    depth + 1))),
            DetectionRuleType.AllOf => rule.Rules.Count == 0
                ? Unknown(CompletionUnknownReason.DatabaseMismatch)
                : EvaluateAllOf(rule.Rules.Select(child => EvaluateRule(
                    child,
                    item,
                    identityResolver,
                    observations,
                    depth + 1))),
            DetectionRuleType.Unsupported => Unknown(CompletionUnknownReason.UnsupportedRule),
            _ => Unknown(CompletionUnknownReason.UnsupportedRule),
        };
    }

    private static RuleEvaluationResult EvaluateIdentitySource(
        DetectionRule rule,
        ReviewedRuntimeItem item,
        IItemIdentityResolver identityResolver,
        ItemObservationSource source)
    {
        if (rule.ParamId is not uint paramId || paramId == 0)
        {
            return Unknown(CompletionUnknownReason.DatabaseMismatch);
        }

        var ruleIdentity = new ItemIdentity(item.SourceKind, paramId);
        if (!identityResolver.TryResolveIdentity(ruleIdentity, out var target) ||
            target != item.Identity)
        {
            return Unknown(CompletionUnknownReason.DatabaseMismatch);
        }

        if (source.Status is ObservationSourceStatus.NotParsed)
        {
            return Unknown(CompletionUnknownReason.SourceNotParsed);
        }

        if (source.Status is ObservationSourceStatus.Corrupt)
        {
            return Unknown(CompletionUnknownReason.SourceCorrupt);
        }

        var hasUnknownIdentity = source.UnresolvedSourceKinds.Contains(target.SourceKind);
        foreach (var observed in source.Values)
        {
            if (identityResolver.TryResolveIdentity(observed, out var canonical))
            {
                if (canonical == target)
                {
                    return Matched();
                }

                continue;
            }

            if (observed.SourceKind == target.SourceKind)
            {
                hasUnknownIdentity = true;
            }
        }

        return hasUnknownIdentity
            ? Unknown(CompletionUnknownReason.UnknownItemId)
            : NotMatched();
    }

    private static RuleEvaluationResult EvaluateEventFlag(
        DetectionRule rule,
        ObservationSource<uint> source)
    {
        if (rule.FlagId is not uint flagId || flagId == 0)
        {
            return Unknown(CompletionUnknownReason.DatabaseMismatch);
        }

        return source.Status switch
        {
            ObservationSourceStatus.NotParsed => Unknown(CompletionUnknownReason.SourceNotParsed),
            ObservationSourceStatus.Corrupt => Unknown(CompletionUnknownReason.SourceCorrupt),
            ObservationSourceStatus.Complete when source.Values.Contains(flagId) => Matched(),
            ObservationSourceStatus.Complete => NotMatched(),
            _ => Unknown(CompletionUnknownReason.SourceNotParsed),
        };
    }

    private static RuleEvaluationResult EvaluateAnyOf(IEnumerable<RuleEvaluationResult> results)
    {
        var materialized = results.ToArray();
        if (materialized.Any(static result => result.State is RuleMatchState.Matched))
        {
            return Matched();
        }

        var unknownReasons = materialized
            .Where(static result => result.State is RuleMatchState.Unknown)
            .SelectMany(static result => result.UnknownReasons)
            .Distinct()
            .Order()
            .ToArray();
        return unknownReasons.Length == 0
            ? NotMatched()
            : new RuleEvaluationResult(RuleMatchState.Unknown, unknownReasons);
    }

    private static RuleEvaluationResult EvaluateAllOf(IEnumerable<RuleEvaluationResult> results)
    {
        var materialized = results.ToArray();
        if (materialized.Any(static result => result.State is RuleMatchState.NotMatched))
        {
            return NotMatched();
        }

        var unknownReasons = materialized
            .Where(static result => result.State is RuleMatchState.Unknown)
            .SelectMany(static result => result.UnknownReasons)
            .Distinct()
            .Order()
            .ToArray();
        return unknownReasons.Length == 0
            ? Matched()
            : new RuleEvaluationResult(RuleMatchState.Unknown, unknownReasons);
    }

    private static RuleEvaluationResult Matched() =>
        new(RuleMatchState.Matched, []);

    private static RuleEvaluationResult NotMatched() =>
        new(RuleMatchState.NotMatched, []);

    private static RuleEvaluationResult Unknown(CompletionUnknownReason reason) =>
        new(RuleMatchState.Unknown, [reason]);
}
