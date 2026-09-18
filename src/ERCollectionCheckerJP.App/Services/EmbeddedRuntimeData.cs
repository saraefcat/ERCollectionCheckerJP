using System.IO;

namespace ERCollectionCheckerJP.App.Services;

internal sealed class EmbeddedRuntimeData : IAsyncDisposable
{
    private const string ResourcePrefix = "ERCollectionCheckerJP.RuntimeData/";
    private const int ExpectedJsonFileCount = 16;
    private static readonly TimeSpan StaleDirectoryAge = TimeSpan.FromHours(24);

    private EmbeddedRuntimeData(string rootDirectory)
    {
        RootDirectory = rootDirectory;
    }

    public string RootDirectory { get; }

    public static async Task<EmbeddedRuntimeData> ExtractAsync(
        CancellationToken cancellationToken = default)
    {
        var assembly = typeof(EmbeddedRuntimeData).Assembly;
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (resourceNames.Length != ExpectedJsonFileCount)
        {
            throw new InvalidDataException(
                $"The embedded Runtime data is incomplete: expected {ExpectedJsonFileCount} files, " +
                $"found {resourceNames.Length}.");
        }

        var parentDirectory = Path.Combine(
            Path.GetTempPath(),
            "ERCollectionCheckerJP",
            "runtime");
        Directory.CreateDirectory(parentDirectory);
        TryHideDirectory(parentDirectory);
        CleanupStaleDirectories(
            parentDirectory,
            DateTimeOffset.UtcNow.Subtract(StaleDirectoryAge));
        var rootDirectory = Path.Combine(parentDirectory, Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(rootDirectory);

            foreach (var resourceName in resourceNames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = ValidateRelativePath(resourceName[ResourcePrefix.Length..]);
                var destinationPath = ResolveDestination(rootDirectory, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

                await using var source = assembly.GetManifestResourceStream(resourceName)
                    ?? throw new InvalidDataException(
                        $"An embedded Runtime resource could not be opened: {resourceName}");
                await using var destination = new FileStream(
                    destinationPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    FileOptions.Asynchronous | FileOptions.WriteThrough);
                await source.CopyToAsync(destination, cancellationToken);
                await destination.FlushAsync(cancellationToken);
            }

            return new EmbeddedRuntimeData(rootDirectory);
        }
        catch
        {
            TryDeleteDirectory(rootDirectory);
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        TryDeleteDirectory(RootDirectory);
        return ValueTask.CompletedTask;
    }

    private static string ValidateRelativePath(string resourcePath)
    {
        var normalized = resourcePath.Replace('\\', '/');
        var segments = normalized.Split('/');
        if (segments.Length < 2 ||
            segments.Any(segment =>
                string.IsNullOrWhiteSpace(segment) || segment is "." or "..") ||
            !string.Equals(Path.GetExtension(normalized), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"An embedded Runtime resource path is invalid: {resourcePath}");
        }

        return normalized;
    }

    private static string ResolveDestination(string rootDirectory, string relativePath)
    {
        var destinationPath = Path.GetFullPath(Path.Combine(
            rootDirectory,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var requiredPrefix = Path.TrimEndingDirectorySeparator(rootDirectory) +
            Path.DirectorySeparatorChar;

        if (!destinationPath.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"An embedded Runtime resource escaped its extraction directory: {relativePath}");
        }

        return destinationPath;
    }

    private static void TryHideDirectory(string directory)
    {
        try
        {
            var attributes = File.GetAttributes(directory);
            File.SetAttributes(directory, attributes | FileAttributes.Hidden);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Hiding is cosmetic. Extraction and cleanup remain mandatory.
        }
    }

    internal static void CleanupStaleDirectories(
        string parentDirectory,
        DateTimeOffset cutoffUtc)
    {
        try
        {
            if (!Directory.Exists(parentDirectory))
            {
                return;
            }

            foreach (var directory in Directory.EnumerateDirectories(parentDirectory))
            {
                var name = Path.GetFileName(directory);
                if (!Guid.TryParseExact(name, "N", out _))
                {
                    continue;
                }

                DateTimeOffset lastWriteUtc;
                try
                {
                    lastWriteUtc = Directory.GetLastWriteTimeUtc(directory);
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    continue;
                }

                if (lastWriteUtc <= cutoffUtc)
                {
                    TryDeleteDirectory(directory);
                }
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Cleanup is best effort and must not prevent the app from starting.
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // A failed best-effort cleanup must not hide the already loaded catalog.
        }
    }
}
