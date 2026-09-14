using ERCollectionCheckerJP.Application.Completion;
using ERCollectionCheckerJP.Domain.Items;
using ERCollectionCheckerJP.SaveParser.Character;
using ERCollectionCheckerJP.SaveParser.Container;
using ERCollectionCheckerJP.SaveParser.IO;

namespace ERCollectionCheckerJP.Application.Analysis;

public sealed class SaveAnalysisService : ISaveAnalysisService
{
    private readonly RuntimeCatalogSnapshot runtimeCatalog;

    public SaveAnalysisService(RuntimeCatalogSnapshot runtimeCatalog)
    {
        ArgumentNullException.ThrowIfNull(runtimeCatalog);
        this.runtimeCatalog = runtimeCatalog;
    }

    public async Task<SaveAnalysisResult> AnalyzeAsync(
        SaveAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SlotIndex is < 0 or >= SaveSlotCompletionEvaluator.CharacterSlotCount)
        {
            throw new CollectionCheckException(
                CollectionCheckErrorCode.InvalidSlot,
                $"The character slot must be between 0 and {SaveSlotCompletionEvaluator.CharacterSlotCount - 1}.");
        }

        var savePath = NormalizeSavePath(request.SavePath);

        var before = await LoadSaveSnapshotAsync(savePath, cancellationToken);
        SaveSlotCompletionEvaluation evaluation;
        try
        {
            evaluation = SaveSlotCompletionEvaluator.EvaluateSave(
                before.Bytes,
                request.SlotIndex,
                runtimeCatalog.Catalog);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is InvalidDataException or
            System.Security.Cryptography.CryptographicException or
            OverflowException or
            ArgumentException)
        {
            throw new CollectionCheckException(
                CollectionCheckErrorCode.InvalidSave,
                "The save file is damaged or uses an unsupported format.",
                exception);
        }

        var after = await LoadSaveSnapshotAsync(savePath, cancellationToken);
        if (!string.Equals(before.Sha256, after.Sha256, StringComparison.Ordinal))
        {
            throw new CollectionCheckException(
                CollectionCheckErrorCode.SaveChangedWhileReading,
                "The save file changed during analysis. Please try again after saving has finished.");
        }

        return BuildResult(savePath, before, evaluation);
    }

    public async Task<IReadOnlyList<SaveSlotDescriptor>> ReadSlotsAsync(
        string savePath,
        CancellationToken cancellationToken = default)
    {
        var normalizedPath = NormalizeSavePath(savePath);
        var before = await LoadSaveSnapshotAsync(normalizedPath, cancellationToken);
        IReadOnlyList<SaveSlotDescriptor> slots;
        try
        {
            var container = EldenRingSaveContainerReader.Read(before.Bytes);
            slots = container.Entries
                .Take(SaveSlotCompletionEvaluator.CharacterSlotCount)
                .Select(entry =>
                {
                    var payload = EldenRingSaveContainerReader.ExtractPayload(
                        before.Bytes,
                        entry);
                    var metadata = CharacterSlotMetadataReader.ReadSlot(payload);
                    return new SaveSlotDescriptor(
                        entry.Index,
                        metadata.SlotVersion,
                        metadata.IsEmpty,
                        metadata.CharacterName);
                })
                .ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is InvalidDataException or
            System.Security.Cryptography.CryptographicException or
            OverflowException or
            ArgumentException)
        {
            throw new CollectionCheckException(
                CollectionCheckErrorCode.InvalidSave,
                "The save file is damaged or uses an unsupported format.",
                exception);
        }

        var after = await LoadSaveSnapshotAsync(normalizedPath, cancellationToken);
        if (!string.Equals(before.Sha256, after.Sha256, StringComparison.Ordinal))
        {
            throw new CollectionCheckException(
                CollectionCheckErrorCode.SaveChangedWhileReading,
                "The save file changed while character slots were being read.");
        }

        return slots;
    }

    private SaveAnalysisResult BuildResult(
        string savePath,
        ReadOnlyFileSnapshot snapshot,
        SaveSlotCompletionEvaluation evaluation)
    {
        var catalog = runtimeCatalog.Catalog;
        var allSummary = CompletionSummaryBuilder.Build(catalog, evaluation.Items);
        var tarnishedSummary = CompletionSummaryBuilder.Build(
            catalog,
            evaluation.Items,
            new HashSet<ContentPack> { ContentPack.TarnishedPack });
        var collectionSummary = CollectionScopeSummaryBuilder.Build(
            catalog,
            evaluation.Items,
            CollectionMode.Collection);
        var strictSummary = CollectionScopeSummaryBuilder.Build(
            catalog,
            evaluation.Items,
            CollectionMode.StrictAllItems);
        var catalogByKey = catalog.Items.ToDictionary(
            static item => item.Data.Key,
            StringComparer.Ordinal);
        var items = evaluation.Items
            .Select(result =>
            {
                var catalogItem = catalogByKey[result.ItemKey];
                var item = catalogItem.Data;
                return new SaveAnalysisItem(
                    item.Key,
                    item.NameJa,
                    item.NameEn,
                    item.ParamId,
                    item.Kind,
                    catalogItem.GoodsCategory,
                    item.ContentPack,
                    item.IsDataOnly,
                    item.ExclusionReason,
                    catalogItem.DetectionCoverage,
                    CollectionScopePolicy.Classify(catalogItem, CollectionMode.Collection),
                    CollectionScopePolicy.Classify(catalogItem, CollectionMode.StrictAllItems),
                    result.State,
                    result.RuleEvaluation.UnknownReasons,
                    result.ArmorCollection?.State,
                    result.ArmorCollection?.PhysicalOwnershipState,
                    result.ArmorCollection?.ConversionSourceItemKey);
            })
            .OrderBy(static item => item.Kind)
            .ThenBy(static item => item.ParamId)
            .ThenBy(static item => item.Key, StringComparer.Ordinal)
            .ToArray();

        return new SaveAnalysisResult(
            new AnalyzedSaveFile(Path.GetFileName(savePath), snapshot.Sha256, true),
            evaluation.SlotIndex,
            evaluation.SlotVersion,
            evaluation.IsEmpty,
            runtimeCatalog.Versions,
            allSummary,
            tarnishedSummary,
            collectionSummary,
            strictSummary,
            items);
    }

    private static async Task<ReadOnlyFileSnapshot> LoadSaveSnapshotAsync(
        string savePath,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ReadOnlyFileSnapshotLoader.LoadAsync(savePath, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (FileNotFoundException exception)
        {
            throw new CollectionCheckException(
                CollectionCheckErrorCode.SaveNotFound,
                "The save file was not found.",
                exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new CollectionCheckException(
                CollectionCheckErrorCode.AccessDenied,
                "The save file could not be read because access was denied.",
                exception);
        }
        catch (IOException exception) when (exception.Message.Contains(
            "changed while it was being read",
            StringComparison.Ordinal))
        {
            throw new CollectionCheckException(
                CollectionCheckErrorCode.SaveChangedWhileReading,
                "The save file changed during analysis. Please try again after saving has finished.",
                exception);
        }
        catch (IOException exception)
        {
            throw new CollectionCheckException(
                CollectionCheckErrorCode.SaveReadFailed,
                "The save file could not be read.",
                exception);
        }
    }

    private static string NormalizeSavePath(string savePath)
    {
        if (string.IsNullOrWhiteSpace(savePath))
        {
            throw new CollectionCheckException(
                CollectionCheckErrorCode.InvalidConfiguration,
                "A save file path is required.");
        }

        try
        {
            return Path.GetFullPath(savePath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            throw new CollectionCheckException(
                CollectionCheckErrorCode.InvalidConfiguration,
                "The save file path is invalid.",
                exception);
        }
    }
}
