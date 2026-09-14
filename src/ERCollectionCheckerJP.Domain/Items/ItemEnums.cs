namespace ERCollectionCheckerJP.Domain.Items;

public enum ItemKind
{
    Unknown,
    Weapon,
    Armor,
    Accessory,
    Goods,
    AshOfWar,
    Sorcery,
    Incantation,
    Gesture,
    TorrentAttire,
}

public enum ContentPack
{
    BaseGame,
    ShadowOfTheErdtree,
    TarnishedPack,
    Unknown,
}

public enum CompletionState
{
    Owned,
    Missing,
    Unknown,
    Excluded,
}

public enum CollectionMode
{
    Collection,
    StrictAllItems,
}

public enum ArmorCollectionState
{
    OwnedExact,
    CoveredByConversion,
    Missing,
    Unknown,
    Excluded,
}

public enum ArmorVariantType
{
    Normal,
    Altered,
    Special,
}

public enum ArmorAcquisitionKind
{
    DirectOnly,
    TransformOnly,
    DirectOrTransform,
    Unknown,
}

public enum GoodsCategory
{
    Unknown,
    General,
    KeyItem,
    Material,
    Remembrance,
    SpiritAsh,
    Flask,
    CrystalTear,
    ReusableContainer,
    Information,
    UpgradeMaterial,
}

public enum DataPresence
{
    Present,
    NotPresent,
}

public enum Obtainability
{
    Obtainable,
    Unobtainable,
    Unknown,
}

public enum ExclusionReason
{
    CutContent,
    DeveloperTest,
    NpcOnly,
    Placeholder,
    InternalDummy,
    Unobtainable,
    ModOnly,
    Other,
}
