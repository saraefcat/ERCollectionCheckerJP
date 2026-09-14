namespace ERCollectionCheckerJP.SaveDiagnostics.Tests;

public sealed class OptionsAndPathSafetyTests
{
    [Fact]
    public void InspectOptions_RequireNamedPairs()
    {
        var options = InspectSaveOptions.Parse(
            ["--save", "baseline.sl2", "--output", "inspection.json"]);

        Assert.Equal("baseline.sl2", options.SavePath);
        Assert.Equal("inspection.json", options.OutputPath);
        Assert.Throws<ArgumentException>(() => InspectSaveOptions.Parse(["--save", "x"]));
        Assert.Throws<ArgumentException>(() => InspectSaveOptions.Parse(
            ["--save", "x", "--output", "y", "--unknown", "z"]));
        Assert.Throws<ArgumentException>(() => InspectSaveOptions.Parse(
            ["--save", "x", "--save", "y", "--output", "z"]));
    }

    [Fact]
    public void CompareOptions_RequireRuntimeDataPack()
    {
        var options = CompareSaveInventoriesOptions.Parse(
        [
            "--before", "before.sl2",
            "--after", "after.sl2",
            "--data-pack", "pack",
            "--output", "comparison.json",
        ]);

        Assert.Equal("pack", options.DataPackDirectory);
        Assert.Throws<ArgumentException>(() => CompareSaveInventoriesOptions.Parse(
        [
            "--before", "before.sl2",
            "--after", "after.sl2",
            "--output", "comparison.json",
        ]));
    }

    [Fact]
    public void EvaluateOptions_RequireAllFivePacksAndValidateSlot()
    {
        var options = EvaluateSaveOptions.Parse(
        [
            "--save", "save.sl2",
            "--reviewed-data-pack", "reviewed",
            "--tarnished-data-pack", "tarnished",
            "--armor-conversion-data-pack", "armor",
            "--gesture-mapping-data-pack", "gestures",
            "--goods-classification-data-pack", "goods",
            "--output", "evaluation.json",
        ]);
        var slotNine = EvaluateSaveOptions.Parse(
        [
            "--save", "save.sl2",
            "--reviewed-data-pack", "reviewed",
            "--tarnished-data-pack", "tarnished",
            "--armor-conversion-data-pack", "armor",
            "--gesture-mapping-data-pack", "gestures",
            "--goods-classification-data-pack", "goods",
            "--slot", "9",
            "--output", "evaluation.json",
        ]);
        var developerMode = EvaluateSaveOptions.Parse(
        [
            "--save", "save.sl2",
            "--reviewed-data-pack", "reviewed",
            "--tarnished-data-pack", "tarnished",
            "--armor-conversion-data-pack", "armor",
            "--gesture-mapping-data-pack", "gestures",
            "--goods-classification-data-pack", "goods",
            "--show-data-only", "true",
            "--output", "evaluation.json",
        ]);

        Assert.Equal(0, options.SlotIndex);
        Assert.Equal("armor", options.ArmorConversionDataPackDirectory);
        Assert.Equal("gestures", options.GestureMappingDataPackDirectory);
        Assert.Equal("goods", options.GoodsClassificationDataPackDirectory);
        Assert.False(options.ShowDataOnlyItems);
        Assert.Equal(9, slotNine.SlotIndex);
        Assert.True(developerMode.ShowDataOnlyItems);
        Assert.Throws<ArgumentException>(() => EvaluateSaveOptions.Parse(
        [
            "--save", "save.sl2",
            "--reviewed-data-pack", "reviewed",
            "--tarnished-data-pack", "tarnished",
            "--armor-conversion-data-pack", "armor",
            "--output", "evaluation.json",
        ]));
        Assert.Throws<ArgumentException>(() => EvaluateSaveOptions.Parse(
        [
            "--save", "save.sl2",
            "--reviewed-data-pack", "reviewed",
            "--tarnished-data-pack", "tarnished",
            "--output", "evaluation.json",
        ]));
        Assert.Throws<ArgumentException>(() => EvaluateSaveOptions.Parse(
        [
            "--save", "save.sl2",
            "--reviewed-data-pack", "reviewed",
            "--tarnished-data-pack", "tarnished",
            "--armor-conversion-data-pack", "armor",
            "--gesture-mapping-data-pack", "gestures",
            "--goods-classification-data-pack", "goods",
            "--slot", "10",
            "--output", "evaluation.json",
        ]));
        Assert.Throws<ArgumentException>(() => EvaluateSaveOptions.Parse(
        [
            "--save", "save.sl2",
            "--reviewed-data-pack", "reviewed",
            "--tarnished-data-pack", "tarnished",
            "--armor-conversion-data-pack", "armor",
            "--gesture-mapping-data-pack", "gestures",
            "--goods-classification-data-pack", "goods",
            "--slot", "not-a-number",
            "--output", "evaluation.json",
        ]));
        Assert.Throws<ArgumentException>(() => EvaluateSaveOptions.Parse(
        [
            "--save", "save.sl2",
            "--reviewed-data-pack", "reviewed",
            "--tarnished-data-pack", "tarnished",
            "--armor-conversion-data-pack", "armor",
            "--gesture-mapping-data-pack", "gestures",
            "--goods-classification-data-pack", "goods",
            "--show-data-only", "yes",
            "--output", "evaluation.json",
        ]));
        Assert.Throws<ArgumentException>(() => EvaluateSaveOptions.Parse(
        [
            "--save", "save.sl2",
            "--reviewed-data-pack", "reviewed",
            "--tarnished-data-pack", "tarnished",
            "--armor-conversion-data-pack", "armor",
            "--gesture-mapping-data-pack", "gestures",
            "--output", "evaluation.json",
        ]));
    }

    [Fact]
    public void PathSafety_RejectsOutputInsideFixtureDirectory()
    {
        var fixtureDirectory = Path.Combine(Path.GetTempPath(), "fixture", "baseline");
        var save = Path.Combine(fixtureDirectory, "ER0000.sl2");

        Assert.Throws<ArgumentException>(() => PathSafety.EnsureOutputOutsideSourceDirectories(
            Path.Combine(fixtureDirectory, "report.json"),
            save));
    }

    [Fact]
    public void PathSafety_AllowsSeparateArtifactDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "fixture", Guid.NewGuid().ToString("N"));
        var save = Path.Combine(root, "baseline", "ER0000.sl2");
        var output = Path.Combine(root, "artifacts", "report.json");

        PathSafety.EnsureOutputOutsideSourceDirectories(output, save);
    }

    [Fact]
    public void PathSafety_RejectsOutputInsideRuntimePackDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "fixture", Guid.NewGuid().ToString("N"));
        var packManifest = Path.Combine(root, "runtime-pack", "manifest.json");

        Assert.Throws<ArgumentException>(() => PathSafety.EnsureOutputOutsideSourceDirectories(
            Path.Combine(root, "runtime-pack", "evaluation.json"),
            packManifest));
    }
}
