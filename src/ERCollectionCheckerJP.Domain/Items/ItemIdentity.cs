namespace ERCollectionCheckerJP.Domain.Items;

/// <summary>
/// Identifies a PARAM row without losing the table that gives the numeric ID its meaning.
/// </summary>
public readonly record struct ItemIdentity(ItemKind SourceKind, uint ParamId);

public interface IItemIdentityResolver
{
    bool TryResolveIdentity(ItemIdentity observed, out ItemIdentity canonical);
}
