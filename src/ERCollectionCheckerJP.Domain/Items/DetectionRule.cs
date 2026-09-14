namespace ERCollectionCheckerJP.Domain.Items;

public enum DetectionRuleType
{
    InventoryItem,
    StorageItem,
    AnyContainerItem,
    GaItem,
    EquippedItem,
    EquippedSpell,
    AttachedAshOfWar,
    EventFlag,
    GestureUnlock,
    TorrentAttireUnlock,
    AnyOf,
    AllOf,
    Unsupported,
}

public sealed record DetectionRule
{
    public required DetectionRuleType Type { get; init; }

    public uint? ParamId { get; init; }

    public uint? FlagId { get; init; }

    public IReadOnlyList<DetectionRule> Rules { get; init; } = [];
}
