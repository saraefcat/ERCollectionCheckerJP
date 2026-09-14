using System.Security.Cryptography;
using System.Text.Json;
using ERCollectionCheckerJP.ItemDatabase.Catalog;
using ERCollectionCheckerJP.ItemDatabase.DataPacks;

namespace ERCollectionCheckerJP.Application.Analysis;

public sealed record RuntimeDataPackPaths(
    string ReviewedItemDatabaseDirectory,
    string TarnishedPackDirectory,
    string ArmorConversionsDirectory,
    string GestureMappingsDirectory,
    string GoodsClassificationsDirectory)
{
    public const string ReviewedItemDatabaseDirectoryName = "reviewed-item-database";
    public const string TarnishedPackDirectoryName = "tarnished-pack";
    public const string ArmorConversionsDirectoryName = "armor-conversions";
    public const string GestureMappingsDirectoryName = "gesture-mappings";
    public const string GoodsClassificationsDirectoryName = "goods-classifications";

    public static RuntimeDataPackPaths FromRoot(string runtimeRoot)
    {
        if (string.IsNullOrWhiteSpace(runtimeRoot))
        {
            throw new CollectionCheckException(
                CollectionCheckErrorCode.InvalidConfiguration,
                "A Runtime data pack root is required.");
        }

        string root;
        try
        {
            root = Path.GetFullPath(runtimeRoot);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            throw new CollectionCheckException(
                CollectionCheckErrorCode.InvalidConfiguration,
                "The Runtime data pack root is invalid.",
                exception);
        }

        return new RuntimeDataPackPaths(
            Path.Combine(root, ReviewedItemDatabaseDirectoryName),
            Path.Combine(root, TarnishedPackDirectoryName),
            Path.Combine(root, ArmorConversionsDirectoryName),
            Path.Combine(root, GestureMappingsDirectoryName),
            Path.Combine(root, GoodsClassificationsDirectoryName));
    }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(ReviewedItemDatabaseDirectory) ||
            string.IsNullOrWhiteSpace(TarnishedPackDirectory) ||
            string.IsNullOrWhiteSpace(ArmorConversionsDirectory) ||
            string.IsNullOrWhiteSpace(GestureMappingsDirectory) ||
            string.IsNullOrWhiteSpace(GoodsClassificationsDirectory))
        {
            throw new CollectionCheckException(
                CollectionCheckErrorCode.InvalidConfiguration,
                "Runtime data pack configuration is incomplete.");
        }
    }
}

public sealed record RuntimeDataPackVersions(
    string ReviewedItemDatabase,
    string TarnishedPack,
    string ArmorConversions,
    string GestureMappings,
    string GoodsClassifications);

public sealed record RuntimeCatalogSnapshot(
    ReviewedItemCatalog Catalog,
    RuntimeDataPackVersions Versions);

public static class RuntimeCatalogLoader
{
    public static async Task<RuntimeCatalogSnapshot> LoadAsync(
        RuntimeDataPackPaths paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        paths.Validate();

        try
        {
            var reviewedTask = ReviewedItemDatabaseDataPackLoader.LoadAsync(
                paths.ReviewedItemDatabaseDirectory,
                cancellationToken);
            var tarnishedTask = TarnishedPackDataPackLoader.LoadAsync(
                paths.TarnishedPackDirectory,
                cancellationToken);
            var armorTask = ArmorConversionDataPackLoader.LoadPrimaryAsync(
                paths.ArmorConversionsDirectory,
                cancellationToken);
            var gesturesTask = GestureMappingDataPackLoader.LoadPrimaryAsync(
                paths.GestureMappingsDirectory,
                cancellationToken);
            var goodsTask = GoodsClassificationDataPackLoader.LoadPrimaryAsync(
                paths.GoodsClassificationsDirectory,
                cancellationToken);

            await Task.WhenAll(
                reviewedTask,
                tarnishedTask,
                armorTask,
                gesturesTask,
                goodsTask);

            var reviewed = await reviewedTask;
            var tarnished = await tarnishedTask;
            var armor = await armorTask;
            var gestures = await gesturesTask;
            var goods = await goodsTask;
            var catalog = ReviewedItemCatalogComposer.Compose(
                reviewed,
                tarnished,
                armor,
                gestures,
                goods);

            return new RuntimeCatalogSnapshot(
                catalog,
                new RuntimeDataPackVersions(
                    reviewed.Manifest.DatabaseVersion,
                    tarnished.Manifest.DatabaseVersion,
                    armor.Manifest.DatabaseVersion,
                    gestures.Manifest.DatabaseVersion,
                    goods.Manifest.DatabaseVersion));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is DirectoryNotFoundException or FileNotFoundException)
        {
            throw new CollectionCheckException(
                CollectionCheckErrorCode.DatabaseNotFound,
                "A required Runtime data pack was not found.",
                exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new CollectionCheckException(
                CollectionCheckErrorCode.AccessDenied,
                "A Runtime data pack could not be read because access was denied.",
                exception);
        }
        catch (Exception exception) when (
            exception is InvalidDataException or
            JsonException or
            CryptographicException or
            IOException or
            OverflowException or
            ArgumentException)
        {
            throw new CollectionCheckException(
                CollectionCheckErrorCode.DatabaseVersionMismatch,
                "The Runtime data packs are invalid or do not belong to the same supported game version.",
                exception);
        }
    }
}
