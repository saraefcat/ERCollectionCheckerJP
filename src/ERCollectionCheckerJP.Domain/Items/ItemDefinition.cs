namespace ERCollectionCheckerJP.Domain.Items;

public sealed record ItemDefinition
{
    public required string Key { get; init; }

    public required uint ParamId { get; init; }

    public required ItemKind Kind { get; init; }

    public required string Category { get; init; }

    public string? SubCategory { get; init; }

    public required string CanonicalKey { get; init; }

    public string? CollectionGroupId { get; init; }

    public required ContentPack ContentPack { get; init; }

    public required DataPresence DataPresence { get; init; }

    public required Obtainability Obtainability { get; init; }

    public ExclusionReason? ExclusionReason { get; init; }

    public bool IncludeInCollectionMode { get; init; }

    public bool IncludeInStrictMode { get; init; }

    public IReadOnlyList<DetectionRule> DetectionRules { get; init; } = [];

    public bool IsDataOnly =>
        DataPresence is DataPresence.Present &&
        Obtainability is Obtainability.Unobtainable;
}
