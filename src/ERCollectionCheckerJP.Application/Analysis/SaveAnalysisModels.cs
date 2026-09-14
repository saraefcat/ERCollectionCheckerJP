using ERCollectionCheckerJP.Application.Completion;
using ERCollectionCheckerJP.Domain.Items;
using ERCollectionCheckerJP.ItemDatabase.Catalog;

namespace ERCollectionCheckerJP.Application.Analysis;

public interface ISaveAnalysisService
{
    Task<IReadOnlyList<SaveSlotDescriptor>> ReadSlotsAsync(
        string savePath,
        CancellationToken cancellationToken = default);

    Task<SaveAnalysisResult> AnalyzeAsync(
        SaveAnalysisRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record SaveAnalysisRequest(
    string SavePath,
    int SlotIndex = 0);

public sealed record SaveSlotDescriptor(
    int SlotIndex,
    uint SlotVersion,
    bool IsEmpty,
    string? CharacterName);

public sealed record AnalyzedSaveFile(
    string FileName,
    string Sha256,
    bool ReadOnlyHashVerified);

public sealed record SaveAnalysisItem(
    string Key,
    string? NameJa,
    string? NameEn,
    uint ParamId,
    ItemKind Kind,
    GoodsCategory? GoodsCategory,
    ContentPack ContentPack,
    bool IsDataOnly,
    ExclusionReason? ExclusionReason,
    DetectionCoverage DetectionCoverage,
    CollectionScopeDisposition CollectionScope,
    CollectionScopeDisposition StrictScope,
    CompletionState State,
    IReadOnlyList<CompletionUnknownReason> UnknownReasons,
    ArmorCollectionState? ArmorState,
    CompletionState? ArmorPhysicalOwnershipState,
    string? ConversionSourceItemKey);

public sealed record SaveAnalysisResult(
    AnalyzedSaveFile Save,
    int SlotIndex,
    uint SlotVersion,
    bool IsEmptySlot,
    RuntimeDataPackVersions DataPackVersions,
    CompletionSummary AllCatalogSummary,
    CompletionSummary TarnishedPackSummary,
    CollectionScopeSummary CollectionModeSummary,
    CollectionScopeSummary StrictModeSummary,
    IReadOnlyList<SaveAnalysisItem> Items);
