using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ERCollectionCheckerJP.Application.Analysis;

namespace ERCollectionCheckerJP.SaveDiagnostics;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NewLine = "\n",
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            WriteHelp();
            return args.Length == 0 ? 2 : 0;
        }

        return await ExecuteSafelyAsync(
            async () => args[0].ToLowerInvariant() switch
            {
                "inspect-save" => await InspectAsync(args[1..]),
                "compare-save-inventories" => await CompareAsync(args[1..]),
                "evaluate-save" => await EvaluateAsync(args[1..]),
                _ => throw new ArgumentException($"Unknown command: {args[0]}"),
            },
            Console.Error);
    }

    internal static async Task<int> ExecuteSafelyAsync(
        Func<Task<int>> operation,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            return await operation();
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            IOException or
            CryptographicException or
            UnauthorizedAccessException or
            CollectionCheckException)
        {
            TryWriteError(error, exception.Message);
            return exception is ArgumentException ? 2 : 1;
        }
        catch (Exception exception)
        {
            TryWriteError(
                error,
                $"Unexpected error ({exception.GetType().Name}): {exception.Message}");
            return 1;
        }
    }

    private static void TryWriteError(TextWriter error, string message)
    {
        try
        {
            error.WriteLine(message);
        }
        catch (Exception)
        {
            // A detached or closing console must not turn an already handled CLI failure
            // into an unhandled CLR exception and a Windows application-error dialog.
        }
    }

    private static async Task<int> InspectAsync(string[] args)
    {
        var options = InspectSaveOptions.Parse(args);
        var inspection = await SaveFixtureInspector.InspectAsync(options);
        await WriteJsonAsync(options.OutputPath, inspection);
        Console.WriteLine(
            $"Validated {inspection.Entries.Count} entries; read-only hash verified: " +
            inspection.ReadOnlyHashVerified);
        return inspection.ReadOnlyHashVerified ? 0 : 1;
    }

    private static async Task<int> CompareAsync(string[] args)
    {
        var options = CompareSaveInventoriesOptions.Parse(args);
        var comparison = await SaveInventoryComparer.CompareAsync(options);
        await WriteJsonAsync(options.OutputPath, comparison);
        Console.WriteLine(
            $"Compared {comparison.Slots.Count} slots; likely target: " +
            (comparison.LikelyTargetSlot?.ToString() ?? "undetermined"));
        return comparison.Before.ReadOnlyHashVerified && comparison.After.ReadOnlyHashVerified ? 0 : 1;
    }

    private static async Task<int> EvaluateAsync(string[] args)
    {
        var options = EvaluateSaveOptions.Parse(args);
        var diagnostic = await SaveCompletionDiagnosticEvaluator.EvaluateAsync(options);
        await WriteJsonAsync(options.OutputPath, diagnostic);
        Console.WriteLine(
            $"Evaluated slot {diagnostic.SlotIndex}; " +
            $"Owned={diagnostic.TarnishedPackSummary.OwnedItemCount}, " +
            $"Missing={diagnostic.TarnishedPackSummary.MissingItemCount}, " +
            $"Unknown={diagnostic.TarnishedPackSummary.UnknownReviewedItemCount}; " +
            $"read-only hash verified: {diagnostic.Save.ReadOnlyHashVerified}");
        return diagnostic.Save.ReadOnlyHashVerified ? 0 : 1;
    }

    private static async Task WriteJsonAsync<T>(string outputPath, T value)
    {
        var path = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var output = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            64 * 1024,
            FileOptions.Asynchronous);
        await JsonSerializer.SerializeAsync(output, value, JsonOptions);
        await output.FlushAsync();
    }

    private static void WriteHelp() => Console.WriteLine(
        """
        ERCollectionCheckerJP.SaveDiagnostics

        inspect-save:
          --save <path>              Read-only ER0000.sl2 fixture
          --output <path>            JSON output outside the fixture directory

        compare-save-inventories:
          --before <path>            Baseline ER0000.sl2 fixture
          --after <path>             Comparison ER0000.sl2 fixture
          --data-pack <directory>    Checker Runtime fragment pack with manifest
          --output <path>            JSON output outside both fixture directories

        evaluate-save:
          --save <path>              Read-only ER0000.sl2 fixture
          --reviewed-data-pack <dir> Reviewed item database Runtime pack
          --tarnished-data-pack <dir> Tarnished Pack reviewed rule pack
          --armor-conversion-data-pack <dir> Reviewed armor conversion Runtime pack
          --gesture-mapping-data-pack <dir> Reviewed gesture mapping Runtime pack
          --goods-classification-data-pack <dir> Reviewed Goods classification Runtime pack
          --slot <0-9>               Optional character slot index; default 0
          --show-data-only <bool>    List reviewed unobtainable rows; default false
          --output <path>            Privacy-safe JSON output outside every input directory
        """);
}
