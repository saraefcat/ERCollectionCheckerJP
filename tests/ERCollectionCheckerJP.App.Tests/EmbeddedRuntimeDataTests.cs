using System.IO;
using ERCollectionCheckerJP.App.Services;
using ERCollectionCheckerJP.Application.Analysis;
using ERCollectionCheckerJP.ItemDatabase.Catalog;

namespace ERCollectionCheckerJP.App.Tests;

public sealed class EmbeddedRuntimeDataTests
{
    [Fact]
    public async Task ExtractAsync_ProvidesValidatedRuntimeDataAndDeletesItOnDispose()
    {
        string root;

        await using (var embedded = await EmbeddedRuntimeData.ExtractAsync())
        {
            root = embedded.RootDirectory;
            Assert.True(Directory.Exists(root));
            Assert.Equal(
                16,
                Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories).Count());

            var snapshot = await RuntimeCatalogLoader.LoadAsync(
                RuntimeDataPackPaths.FromRoot(root));

            Assert.Equal(3_367, snapshot.Catalog.Items.Count);
            Assert.Equal(2_768, snapshot.Catalog.Items.Count(static item =>
                item.DetectionCoverage is DetectionCoverage.Reviewed));
            Assert.Equal(599, snapshot.Catalog.Items.Count(static item =>
                item.DetectionCoverage is DetectionCoverage.Excluded));
        }

        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void CleanupStaleDirectories_DeletesOnlyOldGuidDirectories()
    {
        var parent = Path.Combine(
            Path.GetTempPath(),
            "ERCollectionCheckerJP.Tests",
            Guid.NewGuid().ToString("N"));
        var stale = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        var recent = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        var unrelated = Path.Combine(parent, "keep-me");

        try
        {
            Directory.CreateDirectory(stale);
            Directory.CreateDirectory(recent);
            Directory.CreateDirectory(unrelated);
            Directory.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddDays(-2));

            EmbeddedRuntimeData.CleanupStaleDirectories(
                parent,
                DateTimeOffset.UtcNow.AddDays(-1));

            Assert.False(Directory.Exists(stale));
            Assert.True(Directory.Exists(recent));
            Assert.True(Directory.Exists(unrelated));
        }
        finally
        {
            if (Directory.Exists(parent))
            {
                Directory.Delete(parent, recursive: true);
            }
        }
    }
}
