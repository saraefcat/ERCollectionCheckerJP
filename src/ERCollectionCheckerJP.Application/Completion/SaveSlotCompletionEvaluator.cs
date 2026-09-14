using System.Buffers.Binary;
using ERCollectionCheckerJP.ItemDatabase.Catalog;
using ERCollectionCheckerJP.SaveParser.Container;
using ERCollectionCheckerJP.SaveParser.Flags;
using ERCollectionCheckerJP.SaveParser.Items;

namespace ERCollectionCheckerJP.Application.Completion;

public sealed record SaveSlotCompletionEvaluation(
    int SlotIndex,
    uint SlotVersion,
    bool IsEmpty,
    IReadOnlyList<ItemCompletionResult> Items);

public static class SaveSlotCompletionEvaluator
{
    public const int CharacterSlotCount = 10;

    public static SaveSlotCompletionEvaluation EvaluateSave(
        byte[] saveBytes,
        int slotIndex,
        ReviewedItemCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(saveBytes);
        ArgumentNullException.ThrowIfNull(catalog);
        if (slotIndex is < 0 or >= CharacterSlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }

        var container = EldenRingSaveContainerReader.Read(saveBytes);
        var payload = EldenRingSaveContainerReader.ExtractPayload(
            saveBytes,
            container.Entries[slotIndex]);
        if (payload.Length < sizeof(uint))
        {
            throw new InvalidDataException("Save slot is shorter than the version field.");
        }

        var version = BinaryPrimitives.ReadUInt32LittleEndian(payload);
        if (version == 0)
        {
            return new SaveSlotCompletionEvaluation(
                slotIndex,
                version,
                true,
                CompletionEngine.EvaluateCatalog(catalog, CompletionObservationSnapshot.Empty));
        }

        var gaItems = GaItemSectionReader.ReadSlot(payload);
        var inventory = InventorySectionReader.ReadSlot(payload, gaItems);
        var equipment = EquipmentSectionReader.ReadSlot(payload, gaItems, inventory);
        var equippedSpells = EquippedSpellSectionReader.ReadSlot(payload, inventory);
        var storage = StorageSectionReader.ReadSlot(payload, gaItems, inventory);
        var gestures = GestureSectionReader.ReadSlot(payload, storage);
        var eventFlags = EventFlagsSectionReader.ReadSlot(payload, storage);
        var observations = catalog.GestureMappings is null
            ? CompletionObservationBuilder.FromParsedSections(
                payload,
                gaItems,
                inventory,
                equipment,
                equippedSpells,
                storage,
                eventFlags,
                catalog.EventFlagAddresses.Values)
            : CompletionObservationBuilder.FromParsedSections(
                payload,
                gaItems,
                inventory,
                equipment,
                equippedSpells,
                storage,
                gestures,
                catalog.GestureMappings,
                eventFlags,
                catalog.EventFlagAddresses.Values);
        return new SaveSlotCompletionEvaluation(
            slotIndex,
            version,
            false,
            CompletionEngine.EvaluateCatalog(catalog, observations));
    }
}
