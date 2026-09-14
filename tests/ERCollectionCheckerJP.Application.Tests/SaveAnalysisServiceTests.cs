using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using ERCollectionCheckerJP.Application.Analysis;
using ERCollectionCheckerJP.Application.Completion;
using ERCollectionCheckerJP.ItemDatabase.Catalog;
using ERCollectionCheckerJP.SaveParser.Character;
using ERCollectionCheckerJP.SaveParser.Items;

namespace ERCollectionCheckerJP.Application.Tests;

public sealed class SaveAnalysisServiceTests
{
    private static readonly Lazy<Task<RuntimeCatalogSnapshot>> RuntimeCatalog = new(
        static () => RuntimeCatalogLoader.LoadAsync(RuntimePaths()));

    [Fact]
    public void RuntimeDataPackPaths_FromRoot_UsesTheFiveProductDirectories()
    {
        var root = Path.GetFullPath(Path.Combine("runtime", "1.17"));

        var paths = RuntimeDataPackPaths.FromRoot(root);

        Assert.Equal(
            Path.Combine(root, RuntimeDataPackPaths.ReviewedItemDatabaseDirectoryName),
            paths.ReviewedItemDatabaseDirectory);
        Assert.Equal(
            Path.Combine(root, RuntimeDataPackPaths.TarnishedPackDirectoryName),
            paths.TarnishedPackDirectory);
        Assert.Equal(
            Path.Combine(root, RuntimeDataPackPaths.ArmorConversionsDirectoryName),
            paths.ArmorConversionsDirectory);
        Assert.Equal(
            Path.Combine(root, RuntimeDataPackPaths.GestureMappingsDirectoryName),
            paths.GestureMappingsDirectory);
        Assert.Equal(
            Path.Combine(root, RuntimeDataPackPaths.GoodsClassificationsDirectoryName),
            paths.GoodsClassificationsDirectory);
    }

    [Fact]
    public void RuntimeDataPackPaths_EmptyRootReturnsStableConfigurationError()
    {
        var exception = Assert.Throws<CollectionCheckException>(() =>
            RuntimeDataPackPaths.FromRoot(" "));

        Assert.Equal(CollectionCheckErrorCode.InvalidConfiguration, exception.Code);
    }

    [Fact]
    public async Task RuntimeCatalogLoader_LoadsTheBundledFivePackSnapshot()
    {
        var snapshot = await RuntimeCatalog.Value;

        Assert.Equal(3_367, snapshot.Catalog.Items.Count);
        Assert.Equal(2_768, snapshot.Catalog.Items.Count(static item =>
            item.DetectionCoverage is DetectionCoverage.Reviewed));
        Assert.Equal(599, snapshot.Catalog.Items.Count(static item =>
            item.DetectionCoverage is DetectionCoverage.Excluded));
        Assert.Equal("1.17.goods-classifications.cb355f2b", snapshot.Versions.GoodsClassifications);
        Assert.All(
            new[]
            {
                snapshot.Versions.ReviewedItemDatabase,
                snapshot.Versions.TarnishedPack,
                snapshot.Versions.ArmorConversions,
                snapshot.Versions.GestureMappings,
                snapshot.Versions.GoodsClassifications,
            },
            static version => Assert.False(string.IsNullOrWhiteSpace(version)));
    }

    [Fact]
    public async Task RuntimeCatalogLoader_MissingRootReturnsStableDatabaseError()
    {
        var missingRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var exception = await Assert.ThrowsAsync<CollectionCheckException>(() =>
            RuntimeCatalogLoader.LoadAsync(RuntimeDataPackPaths.FromRoot(missingRoot)));

        Assert.Equal(CollectionCheckErrorCode.DatabaseNotFound, exception.Code);
        Assert.DoesNotContain(missingRoot, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_EmptySlotUsesBundledCatalogAndDoesNotModifySave()
    {
        var path = await WriteTemporarySaveAsync(CreateEmptySave());
        try
        {
            var before = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path)));
            var service = new SaveAnalysisService(await RuntimeCatalog.Value);

            var result = await service.AnalyzeAsync(new SaveAnalysisRequest(path));

            var after = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path)));
            Assert.Equal(before, after);
            Assert.Equal(before, result.Save.Sha256);
            Assert.True(result.Save.ReadOnlyHashVerified);
            Assert.Equal(Path.GetFileName(path), result.Save.FileName);
            Assert.True(result.IsEmptySlot);
            Assert.Equal(3_367, result.Items.Count);
            Assert.Equal(2_768, result.AllCatalogSummary.UnknownReviewedItemCount);
            Assert.Equal(599, result.AllCatalogSummary.ExcludedItemCount);
            Assert.Equal(1_940, result.CollectionModeSummary.IncludedItemCount);
            Assert.Equal(828, result.CollectionModeSummary.UnreviewedItemCount);
            Assert.Equal(1_940, result.CollectionModeSummary.Completion.UnknownReviewedItemCount);
            Assert.Equal(2_768, result.StrictModeSummary.IncludedItemCount);
            Assert.Equal(2_768, result.StrictModeSummary.Completion.UnknownReviewedItemCount);
            Assert.All(result.Items, static item =>
                Assert.True(
                    item.StrictScope is not CollectionScopeDisposition.Excluded || item.IsDataOnly));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadSlotsAsync_ReturnsOccupiedCharacterNamesAndDoesNotModifySave()
    {
        var characterPayload = CreateCharacterSlot("Test Hero NG4");
        var payloads = Enumerable.Range(0, 12)
            .Select(static _ => new byte[sizeof(uint)])
            .ToArray();
        payloads[2] = characterPayload;
        var path = await WriteTemporarySaveAsync(CreateSave(payloads));
        try
        {
            var before = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path)));
            ISaveAnalysisService service = new SaveAnalysisService(await RuntimeCatalog.Value);

            var slots = await service.ReadSlotsAsync(path);

            var after = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path)));
            Assert.Equal(before, after);
            Assert.Equal(10, slots.Count);
            Assert.True(slots[0].IsEmpty);
            Assert.False(slots[2].IsEmpty);
            Assert.Equal(2, slots[2].SlotIndex);
            Assert.Equal("Test Hero NG4", slots[2].CharacterName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task AnalyzeAsync_MissingSaveReturnsStableErrorWithoutPersonalPath()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "ER0000.sl2");
        var service = new SaveAnalysisService(await RuntimeCatalog.Value);

        var exception = await Assert.ThrowsAsync<CollectionCheckException>(() =>
            service.AnalyzeAsync(new SaveAnalysisRequest(path)));

        Assert.Equal(CollectionCheckErrorCode.SaveNotFound, exception.Code);
        Assert.DoesNotContain(path, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_InvalidSlotReturnsStableErrorBeforeReadingSave()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "ER0000.sl2");
        var service = new SaveAnalysisService(await RuntimeCatalog.Value);

        var exception = await Assert.ThrowsAsync<CollectionCheckException>(() =>
            service.AnalyzeAsync(new SaveAnalysisRequest(path, 10)));

        Assert.Equal(CollectionCheckErrorCode.InvalidSlot, exception.Code);
    }

    [Fact]
    public async Task AnalyzeAsync_InvalidSaveReturnsStableErrorAndDoesNotModifyInput()
    {
        var bytes = "not a save"u8.ToArray();
        var path = await WriteTemporarySaveAsync(bytes);
        try
        {
            var service = new SaveAnalysisService(await RuntimeCatalog.Value);

            var exception = await Assert.ThrowsAsync<CollectionCheckException>(() =>
                service.AnalyzeAsync(new SaveAnalysisRequest(path)));

            Assert.Equal(CollectionCheckErrorCode.InvalidSave, exception.Code);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static RuntimeDataPackPaths RuntimePaths() => RuntimeDataPackPaths.FromRoot(
        Path.Combine(AppContext.BaseDirectory, "data", "1.17", "runtime"));

    private static async Task<string> WriteTemporarySaveAsync(byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ERCollectionCheckerJP-{Guid.NewGuid():N}.sl2");
        await File.WriteAllBytesAsync(path, bytes);
        return path;
    }

    private static byte[] CreateEmptySave()
    {
        const int entryCount = 12;
        const int headerEnd = 0x300;
        const int payloadSize = sizeof(uint);
        const int storedSize = 0x10 + payloadSize;
        var bytes = new byte[headerEnd + (entryCount * storedSize)];
        "BND4"u8.CopyTo(bytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x0C, 4), entryCount);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(0x10, 8), 0x40);
        Encoding.ASCII.GetBytes("00000001").CopyTo(bytes, 0x18);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(0x20, 8), 0x20);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(0x28, 8), headerEnd);
        bytes[0x30] = 1;
        bytes[0x31] = 0x20;

        var nameOffset = 0x1C0;
        var dataOffset = headerEnd;
        for (var index = 0; index < entryCount; index++)
        {
            var headerOffset = 0x40 + (index * 0x20);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(headerOffset, 4), 0x50);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(headerOffset + 4, 4), -1);
            BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(headerOffset + 8, 8), storedSize);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(headerOffset + 0x10, 4), dataOffset);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(headerOffset + 0x14, 4), nameOffset);

            var name = Encoding.Unicode.GetBytes($"USER_DATA{index:D3}\0");
            name.CopyTo(bytes, nameOffset);
            nameOffset += name.Length;

            var payload = bytes.AsSpan(dataOffset + 0x10, payloadSize);
            MD5.HashData(payload).CopyTo(bytes, dataOffset);
            dataOffset += storedSize;
        }

        return bytes;
    }

    private static byte[] CreateSave(IReadOnlyList<byte[]> payloads)
    {
        const int entryCount = 12;
        const int headerEnd = 0x300;
        if (payloads.Count != entryCount)
        {
            throw new ArgumentException("A save must contain twelve payloads.", nameof(payloads));
        }

        var bytes = new byte[headerEnd + payloads.Sum(static payload => 0x10 + payload.Length)];
        "BND4"u8.CopyTo(bytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x0C, 4), entryCount);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(0x10, 8), 0x40);
        Encoding.ASCII.GetBytes("00000001").CopyTo(bytes, 0x18);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(0x20, 8), 0x20);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(0x28, 8), headerEnd);
        bytes[0x30] = 1;
        bytes[0x31] = 0x20;

        var nameOffset = 0x1C0;
        var dataOffset = headerEnd;
        for (var index = 0; index < entryCount; index++)
        {
            var payload = payloads[index];
            var storedSize = 0x10 + payload.Length;
            var headerOffset = 0x40 + (index * 0x20);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(headerOffset, 4), 0x50);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(headerOffset + 4, 4), -1);
            BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(headerOffset + 8, 8), storedSize);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(headerOffset + 0x10, 4), dataOffset);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(headerOffset + 0x14, 4), nameOffset);

            var name = Encoding.Unicode.GetBytes($"USER_DATA{index:D3}\0");
            name.CopyTo(bytes, nameOffset);
            nameOffset += name.Length;

            payload.CopyTo(bytes, dataOffset + 0x10);
            MD5.HashData(payload).CopyTo(bytes, dataOffset);
            dataOffset += storedSize;
        }

        return bytes;
    }

    private static byte[] CreateCharacterSlot(string name)
    {
        const int gaItemCount = 5_118;
        const int emptyGaItemSize = 8;
        var gaItemEnd = GaItemSectionReader.StartOffset + (gaItemCount * emptyGaItemSize);
        var nameOffset = gaItemEnd + CharacterSlotMetadataReader.CharacterNameOffsetFromPlayerGameData;
        var slot = new byte[nameOffset +
            ((CharacterSlotMetadataReader.MaximumCharacterNameLength + 1) * sizeof(ushort))];
        BinaryPrimitives.WriteUInt32LittleEndian(slot, 1);
        for (var index = 0; index < gaItemCount; index++)
        {
            var offset = GaItemSectionReader.StartOffset + (index * emptyGaItemSize);
            BinaryPrimitives.WriteUInt32LittleEndian(slot.AsSpan(offset), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(slot.AsSpan(offset + sizeof(uint)), uint.MaxValue);
        }

        Encoding.Unicode.GetBytes(name).CopyTo(slot, nameOffset);
        return slot;
    }
}
