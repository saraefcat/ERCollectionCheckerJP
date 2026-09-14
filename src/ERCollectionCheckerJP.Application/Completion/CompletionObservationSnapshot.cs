using ERCollectionCheckerJP.Domain.Items;

namespace ERCollectionCheckerJP.Application.Completion;

public enum ObservationSourceStatus
{
    NotParsed,
    Complete,
    Corrupt,
}

public sealed record ObservationSource<T>
    where T : notnull
{
    private ObservationSource(ObservationSourceStatus status, IReadOnlySet<T> values)
    {
        Status = status;
        Values = values;
    }

    public ObservationSourceStatus Status { get; }

    public IReadOnlySet<T> Values { get; }

    public static ObservationSource<T> Complete(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new ObservationSource<T>(ObservationSourceStatus.Complete, values.ToHashSet());
    }

    public static ObservationSource<T> NotParsed() =>
        new(ObservationSourceStatus.NotParsed, new HashSet<T>());

    public static ObservationSource<T> Corrupt() =>
        new(ObservationSourceStatus.Corrupt, new HashSet<T>());
}

public sealed record ItemObservationSource
{
    private ItemObservationSource(
        ObservationSourceStatus status,
        IReadOnlySet<ItemIdentity> values,
        IReadOnlySet<ItemKind> unresolvedSourceKinds)
    {
        Status = status;
        Values = values;
        UnresolvedSourceKinds = unresolvedSourceKinds;
    }

    public ObservationSourceStatus Status { get; }

    public IReadOnlySet<ItemIdentity> Values { get; }

    public IReadOnlySet<ItemKind> UnresolvedSourceKinds { get; }

    public static ItemObservationSource Complete(
        IEnumerable<ItemIdentity> values,
        IEnumerable<ItemKind>? unresolvedSourceKinds = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new ItemObservationSource(
            ObservationSourceStatus.Complete,
            values.ToHashSet(),
            unresolvedSourceKinds?.ToHashSet() ?? new HashSet<ItemKind>());
    }

    public static ItemObservationSource NotParsed() => new(
        ObservationSourceStatus.NotParsed,
        new HashSet<ItemIdentity>(),
        new HashSet<ItemKind>());

    public static ItemObservationSource Corrupt() => new(
        ObservationSourceStatus.Corrupt,
        new HashSet<ItemIdentity>(),
        new HashSet<ItemKind>());
}

public sealed record CompletionObservationSnapshot(
    ItemObservationSource Inventory,
    ItemObservationSource Storage,
    ItemObservationSource GaItems,
    ItemObservationSource EquippedItems,
    ItemObservationSource AttachedAshesOfWar,
    ItemObservationSource Gestures,
    ObservationSource<uint> EventFlags)
{
    public ItemObservationSource EquippedSpells { get; init; } =
        ItemObservationSource.NotParsed();

    public static CompletionObservationSnapshot Empty { get; } = new(
        ItemObservationSource.NotParsed(),
        ItemObservationSource.NotParsed(),
        ItemObservationSource.NotParsed(),
        ItemObservationSource.NotParsed(),
        ItemObservationSource.NotParsed(),
        ItemObservationSource.NotParsed(),
        ObservationSource<uint>.NotParsed());
}
