using System.Security.Cryptography;
using ERCollectionCheckerJP.SaveParser.IO;

namespace ERCollectionCheckerJP.SaveParser.Tests;

public sealed class ReadOnlyFileSnapshotLoaderTests
{
    [Fact]
    public async Task LoadAsync_ReadsBytesAndReturnsTheirHash()
    {
        var path = Path.GetTempFileName();
        try
        {
            var expected = "read-only fixture"u8.ToArray();
            await File.WriteAllBytesAsync(path, expected);

            var snapshot = await ReadOnlyFileSnapshotLoader.LoadAsync(path);

            Assert.Equal(expected, snapshot.Bytes);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(expected)), snapshot.Sha256);
            Assert.Equal(expected, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
