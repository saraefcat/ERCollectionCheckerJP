namespace ERCollectionCheckerJP.SaveDiagnostics;

internal static class PathSafety
{
    public static void EnsureOutputOutsideSourceDirectories(
        string outputPath,
        params string[] sourceFiles)
    {
        var output = Path.GetFullPath(outputPath);
        foreach (var sourceFile in sourceFiles)
        {
            var source = Path.GetFullPath(sourceFile);
            var sourceDirectory = Path.GetDirectoryName(source) ??
                throw new ArgumentException($"Source has no parent directory: {source}");
            if (IsWithin(output, sourceDirectory))
            {
                throw new ArgumentException(
                    "Diagnostic output must be outside every input directory.");
            }
        }
    }

    private static bool IsWithin(string path, string directory)
    {
        var prefix = Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar;
        return string.Equals(path, directory, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
