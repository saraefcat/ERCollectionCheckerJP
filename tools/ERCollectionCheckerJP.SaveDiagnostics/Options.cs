namespace ERCollectionCheckerJP.SaveDiagnostics;

public sealed record InspectSaveOptions(string SavePath, string OutputPath)
{
    public static InspectSaveOptions Parse(IReadOnlyList<string> args)
    {
        var values = OptionParser.Parse(args, ["--save", "--output"]);
        return new InspectSaveOptions(
            OptionParser.Required(values, "--save"),
            OptionParser.Required(values, "--output"));
    }
}

public sealed record CompareSaveInventoriesOptions(
    string BeforeSavePath,
    string AfterSavePath,
    string DataPackDirectory,
    string OutputPath)
{
    public static CompareSaveInventoriesOptions Parse(IReadOnlyList<string> args)
    {
        var values = OptionParser.Parse(args, ["--before", "--after", "--data-pack", "--output"]);
        return new CompareSaveInventoriesOptions(
            OptionParser.Required(values, "--before"),
            OptionParser.Required(values, "--after"),
            OptionParser.Required(values, "--data-pack"),
            OptionParser.Required(values, "--output"));
    }
}

public sealed record EvaluateSaveOptions(
    string SavePath,
    string ReviewedDataPackDirectory,
    string TarnishedDataPackDirectory,
    string ArmorConversionDataPackDirectory,
    string GestureMappingDataPackDirectory,
    string GoodsClassificationDataPackDirectory,
    int SlotIndex,
    bool ShowDataOnlyItems,
    string OutputPath)
{
    public static EvaluateSaveOptions Parse(IReadOnlyList<string> args)
    {
        var values = OptionParser.Parse(
            args,
            [
                "--save",
                "--reviewed-data-pack",
                "--tarnished-data-pack",
                "--armor-conversion-data-pack",
                "--gesture-mapping-data-pack",
                "--goods-classification-data-pack",
                "--slot",
                "--show-data-only",
                "--output",
            ]);
        var slotIndex = 0;
        if (values.TryGetValue("--slot", out var slotValue) &&
            (!int.TryParse(slotValue, out slotIndex) || slotIndex is < 0 or >= 10))
        {
            throw new ArgumentException("--slot must be an integer from 0 through 9.");
        }

        var showDataOnly = false;
        if (values.TryGetValue("--show-data-only", out var showDataOnlyValue) &&
            !bool.TryParse(showDataOnlyValue, out showDataOnly))
        {
            throw new ArgumentException("--show-data-only must be true or false.");
        }

        return new EvaluateSaveOptions(
            OptionParser.Required(values, "--save"),
            OptionParser.Required(values, "--reviewed-data-pack"),
            OptionParser.Required(values, "--tarnished-data-pack"),
            OptionParser.Required(values, "--armor-conversion-data-pack"),
            OptionParser.Required(values, "--gesture-mapping-data-pack"),
            OptionParser.Required(values, "--goods-classification-data-pack"),
            slotIndex,
            showDataOnly,
            OptionParser.Required(values, "--output"));
    }
}

internal static class OptionParser
{
    public static IReadOnlyDictionary<string, string> Parse(
        IReadOnlyList<string> args,
        IReadOnlyCollection<string> allowed)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Count; index += 2)
        {
            if (index + 1 >= args.Count || !args[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException("Options must be provided as --name <value> pairs.");
            }

            if (!allowed.Contains(args[index]))
            {
                throw new ArgumentException($"Unknown option: {args[index]}");
            }

            if (!values.TryAdd(args[index], args[index + 1]))
            {
                throw new ArgumentException($"Duplicate option: {args[index]}");
            }
        }

        return values;
    }

    public static string Required(IReadOnlyDictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Missing required option: {key}");
        }

        return value;
    }
}
