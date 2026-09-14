using ERCollectionCheckerJP.Application.Analysis;
using ERCollectionCheckerJP.Application.Completion;
using ERCollectionCheckerJP.Domain.Items;
using ERCollectionCheckerJP.ItemDatabase.Catalog;
using ERCollectionCheckerJP.ItemDatabase.DataPacks;

namespace ERCollectionCheckerJP.SaveDiagnostics;

public sealed record DiagnosticSaveFile(
    string FileName,
    string Sha256,
    bool ReadOnlyHashVerified);

public sealed record DiagnosticCompletionItem(
    string Key,
    string NameJa,
    string NameEn,
    ItemKind Kind,
    GoodsCategory? GoodsCategory,
    ContentPack ContentPack,
    CompletionState State,
    IReadOnlyList<CompletionUnknownReason> UnknownReasons,
    ArmorCollectionState? ArmorState,
    CompletionState? ArmorPhysicalOwnershipState,
    string? ConversionSourceItemKey);

public sealed record DiagnosticDataOnlyItem(
    string Key,
    string? NameJa,
    string? NameEn,
    ItemKind Kind,
    uint ParamId,
    ContentPack ContentPack,
    ExclusionReason ExclusionReason);

public sealed record SaveCompletionDiagnostic(
    int SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    DiagnosticSaveFile Save,
    int SlotIndex,
    uint SlotVersion,
    bool IsEmptySlot,
    string ReviewedDatabaseVersion,
    string TarnishedDatabaseVersion,
    string ArmorConversionDatabaseVersion,
    string GestureMappingDatabaseVersion,
    string GoodsClassificationDatabaseVersion,
    CompletionSummary AllCatalogSummary,
    CompletionSummary TarnishedPackSummary,
    CollectionScopeSummary CollectionModeSummary,
    CollectionScopeSummary StrictModeSummary,
    IReadOnlyList<DiagnosticCompletionItem> ReviewedItems,
    IReadOnlyList<DiagnosticDataOnlyItem> DataOnlyItems);

public static class SaveCompletionDiagnosticEvaluator
{
    public static async Task<SaveCompletionDiagnostic> EvaluateAsync(
        EvaluateSaveOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        PathSafety.EnsureOutputOutsideSourceDirectories(
            options.OutputPath,
            options.SavePath,
            Path.Combine(options.ReviewedDataPackDirectory, ReviewedItemDatabaseDataPackLayout.ManifestFileName),
            Path.Combine(options.TarnishedDataPackDirectory, TarnishedPackDataPackLayout.ManifestFileName),
            Path.Combine(
                options.ArmorConversionDataPackDirectory,
                ArmorConversionDataPackLayout.ManifestFileName),
            Path.Combine(
                options.GestureMappingDataPackDirectory,
                GestureMappingDataPackLayout.ManifestFileName),
            Path.Combine(
                options.GoodsClassificationDataPackDirectory,
                GoodsClassificationDataPackLayout.ManifestFileName));

        var runtimeCatalog = await RuntimeCatalogLoader.LoadAsync(
            new RuntimeDataPackPaths(
                options.ReviewedDataPackDirectory,
                options.TarnishedDataPackDirectory,
                options.ArmorConversionDataPackDirectory,
                options.GestureMappingDataPackDirectory,
                options.GoodsClassificationDataPackDirectory),
            cancellationToken);
        var analysis = await new SaveAnalysisService(runtimeCatalog).AnalyzeAsync(
            new SaveAnalysisRequest(options.SavePath, options.SlotIndex),
            cancellationToken);

        var reviewedItems = analysis.Items
            .Where(static item => item.DetectionCoverage is DetectionCoverage.Reviewed)
            .Select(item => new DiagnosticCompletionItem(
                item.Key,
                item.NameJa ?? throw new InvalidDataException(
                    $"Reviewed item has no Japanese name: {item.Key}"),
                item.NameEn ?? throw new InvalidDataException(
                    $"Reviewed item has no English name: {item.Key}"),
                item.Kind,
                item.GoodsCategory,
                item.ContentPack,
                item.State,
                item.UnknownReasons,
                item.ArmorState,
                item.ArmorPhysicalOwnershipState,
                item.ConversionSourceItemKey))
            .ToArray();
        var dataOnlyItems = analysis.Items
            .Where(item => item.IsDataOnly && options.ShowDataOnlyItems)
            .Select(item => new DiagnosticDataOnlyItem(
                item.Key,
                item.NameJa,
                item.NameEn,
                item.Kind,
                item.ParamId,
                item.ContentPack,
                item.ExclusionReason ?? throw new InvalidDataException(
                    $"Data-only item has no exclusion reason: {item.Key}")))
            .OrderBy(static item => item.Kind)
            .ThenBy(static item => item.ParamId)
            .ToArray();

        return new SaveCompletionDiagnostic(
            6,
            DateTimeOffset.UtcNow,
            new DiagnosticSaveFile(
                analysis.Save.FileName,
                analysis.Save.Sha256,
                analysis.Save.ReadOnlyHashVerified),
            analysis.SlotIndex,
            analysis.SlotVersion,
            analysis.IsEmptySlot,
            analysis.DataPackVersions.ReviewedItemDatabase,
            analysis.DataPackVersions.TarnishedPack,
            analysis.DataPackVersions.ArmorConversions,
            analysis.DataPackVersions.GestureMappings,
            analysis.DataPackVersions.GoodsClassifications,
            analysis.AllCatalogSummary,
            analysis.TarnishedPackSummary,
            analysis.CollectionModeSummary,
            analysis.StrictModeSummary,
            reviewedItems,
            dataOnlyItems);
    }
}
