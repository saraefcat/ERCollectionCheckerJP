namespace ERCollectionCheckerJP.App.Services;

public interface IUserSettingsService
{
    UserSettings Load();

    bool TrySave(UserSettings settings);
}

public sealed record UserSettings(string? SaveFilePath = null)
{
    public static UserSettings Default { get; } = new();
}

internal sealed class NullUserSettingsService : IUserSettingsService
{
    public static NullUserSettingsService Instance { get; } = new();

    public UserSettings Load() => UserSettings.Default;

    public bool TrySave(UserSettings settings) => false;
}
