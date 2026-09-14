using ERCollectionCheckerJP.Domain.Items;
using ERCollectionCheckerJP.ItemDatabase.Catalog;
using ERCollectionCheckerJP.ItemDatabase.DataPacks;
using ERCollectionCheckerJP.SaveParser.Flags;
using ERCollectionCheckerJP.SaveParser.Items;

namespace ERCollectionCheckerJP.Application.Completion;

public static class CompletionObservationBuilder
{
    /// <summary>
    /// Converts already validated parser sections into immutable rule observations. Sections that the parser
    /// does not support yet remain NotParsed and therefore cannot produce a Missing decision.
    /// </summary>
    public static CompletionObservationSnapshot FromParsedSections(
        ReadOnlySpan<byte> slot,
        GaItemSection gaItems,
        InventorySection inventory,
        StorageSection storage,
        EventFlagsSection eventFlags,
        IEnumerable<EventFlagAddressFragmentEntry> reviewedEventFlagAddresses) =>
        BuildFromParsedSections(
            slot,
            gaItems,
            inventory,
            null,
            null,
            storage,
            null,
            null,
            eventFlags,
            reviewedEventFlagAddresses);

    public static CompletionObservationSnapshot FromParsedSections(
        ReadOnlySpan<byte> slot,
        GaItemSection gaItems,
        InventorySection inventory,
        EquipmentSection equipment,
        StorageSection storage,
        EventFlagsSection eventFlags,
        IEnumerable<EventFlagAddressFragmentEntry> reviewedEventFlagAddresses) =>
        BuildFromParsedSections(
            slot,
            gaItems,
            inventory,
            (EquipmentSection?)equipment,
            null,
            storage,
            null,
            null,
            eventFlags,
            reviewedEventFlagAddresses);

    public static CompletionObservationSnapshot FromParsedSections(
        ReadOnlySpan<byte> slot,
        GaItemSection gaItems,
        InventorySection inventory,
        EquipmentSection equipment,
        EquippedSpellSection equippedSpells,
        StorageSection storage,
        EventFlagsSection eventFlags,
        IEnumerable<EventFlagAddressFragmentEntry> reviewedEventFlagAddresses) =>
        BuildFromParsedSections(
            slot,
            gaItems,
            inventory,
            equipment,
            equippedSpells,
            storage,
            null,
            null,
            eventFlags,
            reviewedEventFlagAddresses);

    public static CompletionObservationSnapshot FromParsedSections(
        ReadOnlySpan<byte> slot,
        GaItemSection gaItems,
        InventorySection inventory,
        EquipmentSection equipment,
        EquippedSpellSection equippedSpells,
        StorageSection storage,
        GestureSection gestures,
        GestureMappingIndex gestureMappings,
        EventFlagsSection eventFlags,
        IEnumerable<EventFlagAddressFragmentEntry> reviewedEventFlagAddresses) =>
        BuildFromParsedSections(
            slot,
            gaItems,
            inventory,
            equipment,
            equippedSpells,
            storage,
            gestures,
            gestureMappings,
            eventFlags,
            reviewedEventFlagAddresses);

    private static CompletionObservationSnapshot BuildFromParsedSections(
        ReadOnlySpan<byte> slot,
        GaItemSection gaItems,
        InventorySection inventory,
        EquipmentSection? equipment,
        EquippedSpellSection? equippedSpells,
        StorageSection storage,
        GestureSection? gestures,
        GestureMappingIndex? gestureMappings,
        EventFlagsSection eventFlags,
        IEnumerable<EventFlagAddressFragmentEntry> reviewedEventFlagAddresses)
    {
        ArgumentNullException.ThrowIfNull(gaItems);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(eventFlags);
        ArgumentNullException.ThrowIfNull(reviewedEventFlagAddresses);

        var flagIds = new HashSet<uint>();
        foreach (var address in reviewedEventFlagAddresses)
        {
            if (EventFlagsSectionReader.IsSet(
                slot,
                eventFlags,
                new EventFlagAddress(address.FlagId, address.ByteIndex, address.BitIndex)))
            {
                flagIds.Add(address.FlagId);
            }
        }

        return new CompletionObservationSnapshot(
            BuildContainerSource(inventory.Records),
            BuildContainerSource(storage.Records),
            ItemObservationSource.Complete(gaItems.Records
                .Where(static record => record.ParamId is not null)
                .Select(static record => new ItemIdentity(
                    record.Kind,
                    NormalizeObservedParamId(record.Kind, record.ParamId!.Value)))),
            equipment is null
                ? ItemObservationSource.NotParsed()
                : BuildEquipmentSource(equipment.Records),
            BuildAttachedAshOfWarSource(gaItems),
            gestures is null || gestureMappings is null
                ? ItemObservationSource.NotParsed()
                : BuildGestureSource(gestures, gestureMappings),
            ObservationSource<uint>.Complete(flagIds))
        {
            EquippedSpells = equippedSpells is null
                ? ItemObservationSource.NotParsed()
                : ItemObservationSource.Complete(equippedSpells.Records.Select(static record =>
                    new ItemIdentity(ItemKind.Goods, record.SpellId))),
        };
    }

    private static ItemObservationSource BuildGestureSource(
        GestureSection gestures,
        GestureMappingIndex gestureMappings)
    {
        var identities = new HashSet<ItemIdentity>();
        foreach (var record in gestures.Records)
        {
            if (gestureMappings.TryResolve(record.SaveGestureId, out var identity))
            {
                identities.Add(identity);
            }
        }

        return ItemObservationSource.Complete(identities);
    }

    private static ItemObservationSource BuildEquipmentSource(
        IEnumerable<EquippedItemRecord> records)
    {
        var identities = new HashSet<ItemIdentity>();
        var unresolvedKinds = new HashSet<ItemKind>();
        foreach (var record in records)
        {
            if (record.ParamId is uint paramId && paramId > 0)
            {
                identities.Add(new ItemIdentity(
                    record.Kind,
                    NormalizeObservedParamId(record.Kind, paramId)));
            }
            else
            {
                unresolvedKinds.Add(record.Kind);
            }
        }

        return ItemObservationSource.Complete(identities, unresolvedKinds);
    }

    private static ItemObservationSource BuildAttachedAshOfWarSource(GaItemSection gaItems)
    {
        var identities = new HashSet<ItemIdentity>();
        var hasUnresolvedHandle = false;
        foreach (var weapon in gaItems.Records.Where(static record => record.Kind is ItemKind.Weapon))
        {
            if (weapon.AttachedAshOfWarHandle is not uint handle || handle is 0 or uint.MaxValue)
            {
                continue;
            }

            if (!gaItems.HandleToRawItemId.TryGetValue(handle, out var rawItemId) ||
                GaItemSectionReader.DecodeKind(rawItemId) is not ItemKind.AshOfWar)
            {
                hasUnresolvedHandle = true;
                continue;
            }

            identities.Add(new ItemIdentity(ItemKind.AshOfWar, rawItemId & 0x0FFF_FFFF));
        }

        return ItemObservationSource.Complete(
            identities,
            hasUnresolvedHandle ? [ItemKind.AshOfWar] : []);
    }

    private static ItemObservationSource BuildContainerSource(
        IEnumerable<InventoryItemRecord> records)
    {
        var identities = new HashSet<ItemIdentity>();
        var unresolvedKinds = new HashSet<ItemKind>();
        foreach (var record in records.Where(static record => record.Quantity > 0))
        {
            if (record.ParamId is uint paramId && paramId > 0)
            {
                identities.Add(new ItemIdentity(
                    record.Kind,
                    NormalizeObservedParamId(record.Kind, paramId)));
            }
            else
            {
                unresolvedKinds.Add(record.Kind);
            }
        }

        return ItemObservationSource.Complete(identities, unresolvedKinds);
    }

    private static uint NormalizeObservedParamId(ItemKind kind, uint paramId) =>
        kind is ItemKind.Weapon ? paramId - (paramId % 100) : paramId;
}
