using System.Security.Cryptography;

namespace ERCollectionCheckerJP.SaveParser.IO;

public sealed record ReadOnlyFileSnapshot(
    byte[] Bytes,
    string Sha256,
    DateTime LastWriteTimeUtc);

public static class ReadOnlyFileSnapshotLoader
{
    public static async Task<ReadOnlyFileSnapshot> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var before = new FileInfo(fullPath);
            before.Refresh();

            if (!before.Exists)
            {
                throw new FileNotFoundException("The source file was not found.", fullPath);
            }

            byte[] bytes;
            await using (var stream = new FileStream(
                             fullPath,
                             FileMode.Open,
                             FileAccess.Read,
                             FileShare.ReadWrite | FileShare.Delete,
                             bufferSize: 1024 * 1024,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                if (stream.Length > int.MaxValue)
                {
                    throw new IOException("The source file is too large to snapshot in memory.");
                }

                bytes = new byte[stream.Length];
                await stream.ReadExactlyAsync(bytes, cancellationToken);
            }

            var after = new FileInfo(fullPath);
            after.Refresh();

            if (before.Length == after.Length &&
                before.LastWriteTimeUtc == after.LastWriteTimeUtc)
            {
                return new ReadOnlyFileSnapshot(
                    bytes,
                    Convert.ToHexString(SHA256.HashData(bytes)),
                    after.LastWriteTimeUtc);
            }

            if (attempt == 3)
            {
                break;
            }
        }

        throw new IOException("The source file changed while it was being read.");
    }
}
