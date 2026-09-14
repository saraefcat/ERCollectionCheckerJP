using System.IO;
using Microsoft.Win32;

namespace ERCollectionCheckerJP.App.Services;

public interface ISaveFileDialogService
{
    string? SelectSaveFile(string? currentPath, string title);
}

public sealed class SaveFileDialogService : ISaveFileDialogService
{
    public string? SelectSaveFile(string? currentPath, string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "ELDEN RING Save (ER0000.sl2)|ER0000.sl2|Save files (*.sl2)|*.sl2|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
            FileName = "ER0000.sl2",
        };
        var initialDirectory = InitialDirectory(currentPath);
        if (initialDirectory is not null)
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog() is true ? dialog.FileName : null;
    }

    private static string? InitialDirectory(string? currentPath)
    {
        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            var directory = Path.GetDirectoryName(currentPath);
            if (Directory.Exists(directory))
            {
                return directory;
            }
        }

        var candidate = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EldenRing");
        return Directory.Exists(candidate) ? candidate : null;
    }
}
