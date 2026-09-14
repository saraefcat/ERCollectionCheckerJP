using System.Buffers.Binary;
using ERCollectionCheckerJP.Domain.Items;
using ERCollectionCheckerJP.SaveParser.Items;

namespace ERCollectionCheckerJP.SaveParser.Tests;

public sealed class EquipmentSectionReaderTests
{
    private const int GaItemEnd = 0x40;
    private const int ItemIdsOffset = GaItemEnd + 0x1B0 + 0xD0 + 0x58 + 0x1C;
    private const int HandlesOffset = ItemIdsOffset + EquipmentSectionReader.TableSize;
    private const int InventoryStartOffset = HandlesOffset + EquipmentSectionReader.TableSize + 4;

    [Fact]
    public void ReadSlot_ParsesKnownWeaponArmorAndTalismanSlots()
    {
        const uint weaponParamId = 2_000_025;
        const uint armorParamId = 5_340_000;
        const uint talismanParamId = 1_234;
        const uint weaponHandle = 0x8000_0001;
        const uint armorHandle = 0x9000_0002;
        const uint talismanHandle = 0xA000_04D2;
        var slot = CreateEmptySlot();
        WriteSlot(slot, 1, weaponParamId, weaponHandle);
        WriteSlot(slot, 12, armorParamId | 0x8000_0000, armorHandle);
        WriteSlot(slot, 17, talismanParamId, talismanHandle);
        var gaItems = GaItems(new Dictionary<uint, uint>
        {
            [weaponHandle] = weaponParamId,
            [armorHandle] = 0x1000_0000 | armorParamId,
        });

        var result = EquipmentSectionReader.ReadSlot(slot, gaItems, Inventory());

        Assert.Equal(ItemIdsOffset, result.ItemIdsOffset);
        Assert.Equal(HandlesOffset, result.HandlesOffset);
        Assert.Equal(InventoryStartOffset - 4, result.EndOffset);
        Assert.Collection(
            result.Records,
            weapon =>
            {
                Assert.Equal(EquippedItemSlot.RightHandArmament1, weapon.Slot);
                Assert.Equal(ItemKind.Weapon, weapon.Kind);
                Assert.Equal(weaponParamId, weapon.ParamId);
                Assert.Equal(InventoryItemResolution.GaItemMap, weapon.Resolution);
            },
            armor =>
            {
                Assert.Equal(EquippedItemSlot.Head, armor.Slot);
                Assert.Equal(ItemKind.Armor, armor.Kind);
                Assert.Equal(armorParamId, armor.ParamId);
            },
            talisman =>
            {
                Assert.Equal(EquippedItemSlot.Talisman1, talisman.Slot);
                Assert.Equal(ItemKind.Accessory, talisman.Kind);
                Assert.Equal(talismanParamId, talisman.ParamId);
                Assert.Equal(InventoryItemResolution.EncodedInHandle, talisman.Resolution);
            });
    }

    [Fact]
    public void ReadSlot_PreservesUnresolvedInstanceAsUnknownKindObservation()
    {
        var slot = CreateEmptySlot();
        WriteSlot(slot, 0, 2_000_000, 0x8000_0001);

        var result = EquipmentSectionReader.ReadSlot(
            slot,
            GaItems(new Dictionary<uint, uint>()),
            Inventory());

        var weapon = Assert.Single(result.Records);
        Assert.Null(weapon.RawItemId);
        Assert.Null(weapon.ParamId);
        Assert.Equal(InventoryItemResolution.Unresolved, weapon.Resolution);
    }

    [Fact]
    public void ReadSlot_RejectsMismatchedItemIdAndResolvedHandle()
    {
        const uint handle = 0x8000_0001;
        var slot = CreateEmptySlot();
        WriteSlot(slot, 0, 2_000_000, handle);

        Assert.Throws<InvalidDataException>(() => EquipmentSectionReader.ReadSlot(
            slot,
            GaItems(new Dictionary<uint, uint> { [handle] = 3_000_000 }),
            Inventory()));
    }

    [Fact]
    public void ReadSlot_RejectsHandleKindThatDoesNotMatchSlot()
    {
        var slot = CreateEmptySlot();
        WriteSlot(slot, 0, 2_000_000, 0x9000_0001);

        Assert.Throws<InvalidDataException>(() => EquipmentSectionReader.ReadSlot(
            slot,
            GaItems(new Dictionary<uint, uint>()),
            Inventory()));
    }

    [Fact]
    public void ReadSlot_RejectsInconsistentEmptyPair()
    {
        var slot = CreateEmptySlot();
        WriteSlot(slot, 0, 2_000_000, 0);

        Assert.Throws<InvalidDataException>(() => EquipmentSectionReader.ReadSlot(
            slot,
            GaItems(new Dictionary<uint, uint>()),
            Inventory()));
    }

    [Fact]
    public void ReadSlot_RejectsInventoryBoundaryMismatch()
    {
        var slot = CreateEmptySlot();
        var inventory = Inventory() with { StartOffset = InventoryStartOffset + 1 };

        Assert.Throws<InvalidDataException>(() => EquipmentSectionReader.ReadSlot(
            slot,
            GaItems(new Dictionary<uint, uint>()),
            inventory));
    }

    private static byte[] CreateEmptySlot()
    {
        var slot = new byte[InventoryStartOffset + 16];
        for (var index = 0; index < EquipmentSectionReader.SlotCount; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(
                slot.AsSpan(ItemIdsOffset + (index * sizeof(uint)), sizeof(uint)),
                uint.MaxValue);
        }

        return slot;
    }

    private static void WriteSlot(byte[] slot, int index, uint itemId, uint handle)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(
            slot.AsSpan(ItemIdsOffset + (index * sizeof(uint)), sizeof(uint)),
            itemId);
        BinaryPrimitives.WriteUInt32LittleEndian(
            slot.AsSpan(HandlesOffset + (index * sizeof(uint)), sizeof(uint)),
            handle);
    }

    private static GaItemSection GaItems(IReadOnlyDictionary<uint, uint> handleMap) =>
        new(0x20, GaItemEnd, [], handleMap);

    private static InventorySection Inventory() =>
        new(0, InventoryStartOffset, 0, 0, 0, 0, 0, []);
}
