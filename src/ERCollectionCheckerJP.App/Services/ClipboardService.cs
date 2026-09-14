using System.Runtime.InteropServices;
using System.Windows;

namespace ERCollectionCheckerJP.App.Services;

public interface IClipboardService
{
    bool TrySetText(string text);
}

public sealed class ClipboardService : IClipboardService
{
    public bool TrySetText(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        try
        {
            Clipboard.SetText(text);
            return true;
        }
        catch (Exception exception) when (
            exception is COMException or ExternalException)
        {
            return false;
        }
    }
}

internal sealed class NullClipboardService : IClipboardService
{
    public static NullClipboardService Instance { get; } = new();

    public bool TrySetText(string text) => false;
}
