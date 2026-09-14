using System.IO;
using ERCollectionCheckerJP.App.Services;

namespace ERCollectionCheckerJP.App.Tests;

public sealed class JsonUserSettingsServiceTests
{
    [Fact]
    public void TrySaveAndLoad_RoundTripsSaveFilePath()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var settingsPath = Path.Combine(directory, "settings.json");
            var savePath = Path.Combine(directory, "ER0000.sl2");
            var service = new JsonUserSettingsService(settingsPath);

            Assert.True(service.TrySave(new UserSettings(savePath)));

            Assert.Equal(Path.GetFullPath(savePath), service.Load().SaveFilePath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_ReturnsDefaultForMalformedDocument()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var settingsPath = Path.Combine(directory, "settings.json");
            File.WriteAllText(settingsPath, "{not-json");

            var actual = new JsonUserSettingsService(settingsPath).Load();

            Assert.Equal(UserSettings.Default, actual);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ERCollectionCheckerJP-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
