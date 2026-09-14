using System.IO;
using ERCollectionCheckerJP.App.Services;
using ERCollectionCheckerJP.App.ViewModels;
using ERCollectionCheckerJP.Application.Analysis;
using ERCollectionCheckerJP.Application.Completion;
using ERCollectionCheckerJP.Domain.Items;
using ERCollectionCheckerJP.ItemDatabase.Catalog;

namespace ERCollectionCheckerJP.App.Tests;

public sealed class MainWindowViewModelTests
{
    private static readonly Lazy<Task<RuntimeCatalogSnapshot>> RuntimeCatalog = new(
        static () => RuntimeCatalogLoader.LoadAsync(RuntimePaths()));

    [Fact]
    public void MainWindow_ShowsAndLaysOutOnSta()
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new MainWindow(
                    new StubSaveAnalysisService([]),
                    new StubUserSettingsService(),
                    new StubClipboardService());
                window.Show();
                window.UpdateLayout();
                window.Close();
            }
            catch (Exception exception)
            {
                captured = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);

        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "WPF window construction timed out.");
        if (captured is not null)
        {
            throw new InvalidOperationException("WPF window construction failed.", captured);
        }
    }

    [Fact]
    public void LanguageSwitch_KeepsDynamicComboBoxSelectionsInWindow()
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                var slots = new StubSaveAnalysisService(
                [
                    new SaveSlotDescriptor(0, 261, false, "Test Hero"),
                ]);
                var viewModel = new MainWindowViewModel(
                    slots,
                    new NullSaveFileDialogService(),
                    new StubUserSettingsService(),
                    new StubClipboardService())
                {
                    SavePath = @"C:\fixture\ER0000.sl2",
                };
                viewModel.InitializeAsync().GetAwaiter().GetResult();

                var window = new MainWindow(
                    new StubSaveAnalysisService([]),
                    new StubUserSettingsService(),
                    new StubClipboardService())
                {
                    DataContext = viewModel,
                };
                window.Show();
                window.UpdateLayout();

                viewModel.Language = UiLanguage.English;
                window.UpdateLayout();

                var character = Assert.IsType<System.Windows.Controls.ComboBox>(
                    window.FindName("CharacterComboBox"));
                var state = Assert.IsType<System.Windows.Controls.ComboBox>(
                    window.FindName("StateFilterComboBox"));
                var category = Assert.IsType<System.Windows.Controls.ComboBox>(
                    window.FindName("CategoryFilterComboBox"));
                var mode = Assert.IsType<System.Windows.Controls.ComboBox>(
                    window.FindName("CollectionModeComboBox"));
                var items = Assert.IsType<System.Windows.Controls.DataGrid>(
                    window.FindName("ItemDataGrid"));
                Assert.NotNull(character.SelectedItem);
                Assert.NotNull(state.SelectedItem);
                Assert.NotNull(category.SelectedItem);
                Assert.NotNull(mode.SelectedItem);
                Assert.Equal(0, character.SelectedValue);
                Assert.Equal(SaveAnalysisStateFilter.Missing, state.SelectedValue);
                Assert.Equal(SaveAnalysisCategory.All, category.SelectedValue);
                Assert.Equal(CollectionMode.Collection, mode.SelectedValue);
                Assert.Equal(
                    System.Windows.Controls.DataGridClipboardCopyMode.None,
                    items.ClipboardCopyMode);
                Assert.Contains(
                    items.InputBindings.Cast<System.Windows.Input.InputBinding>(),
                    binding => binding is System.Windows.Input.KeyBinding keyBinding &&
                        keyBinding.Key is System.Windows.Input.Key.C &&
                        keyBinding.Modifiers is System.Windows.Input.ModifierKeys.Control);

                window.Close();
            }
            catch (Exception exception)
            {
                captured = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);

        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "WPF language-switch test timed out.");
        if (captured is not null)
        {
            throw new InvalidOperationException("WPF language-switch test failed.", captured);
        }
    }

    [Fact]
    public void ItemRow_LocalizesBothNamesStateAndCategory()
    {
        var item = Item() with
        {
            NameJa = "ロングソード",
            NameEn = "Longsword",
        };

        var japanese = new ItemRowViewModel(
            item,
            UiLanguage.Japanese,
            CollectionMode.Collection);
        var english = new ItemRowViewModel(
            item,
            UiLanguage.English,
            CollectionMode.Collection);

        Assert.Equal("ロングソード", japanese.PrimaryName);
        Assert.Equal("Longsword", japanese.SecondaryName);
        Assert.Equal("未所持", japanese.StateText);
        Assert.Equal("武器", japanese.CategoryText);
        Assert.Equal("Longsword", english.PrimaryName);
        Assert.Equal("ロングソード", english.SecondaryName);
        Assert.Equal("Missing", english.StateText);
        Assert.Equal("Weapons", english.CategoryText);
    }

    [Fact]
    public void ItemRow_UsesTheSelectedModeForScopeDetails()
    {
        var item = Item() with
        {
            CollectionScope = CollectionScopeDisposition.Unreviewed,
            StrictScope = CollectionScopeDisposition.Included,
        };

        var collection = new ItemRowViewModel(
            item,
            UiLanguage.English,
            CollectionMode.Collection);
        var strict = new ItemRowViewModel(
            item,
            UiLanguage.English,
            CollectionMode.StrictAllItems);

        Assert.Equal("Collection classification pending", collection.ScopeText);
        Assert.Equal("Included in Strict", strict.ScopeText);
    }

    [Fact]
    public async Task DataOnlySwitch_SelectsAndRestoresTheUsefulStateFilter()
    {
        var snapshot = await RuntimeCatalog.Value;
        var viewModel = new MainWindowViewModel(
            new SaveAnalysisService(snapshot),
            new NullSaveFileDialogService());

        viewModel.ShowDataOnlyItems = true;
        Assert.False(viewModel.ShowDataOnlyItems);

        viewModel.DeveloperMode = true;
        viewModel.ShowDataOnlyItems = true;

        Assert.Equal(SaveAnalysisStateFilter.Excluded, viewModel.SelectedStateFilter);

        viewModel.DeveloperMode = false;

        Assert.False(viewModel.ShowDataOnlyItems);
        Assert.Equal(SaveAnalysisStateFilter.Missing, viewModel.SelectedStateFilter);
    }

    [Fact]
    public async Task CharacterLoading_ShowsOnlyOccupiedSlotsAndLocalizesTheirNames()
    {
        var viewModel = new MainWindowViewModel(
            new StubSaveAnalysisService(
            [
                new SaveSlotDescriptor(0, 261, false, "Test Hero"),
                new SaveSlotDescriptor(1, 0, true, null),
                new SaveSlotDescriptor(2, 251, false, "Second Hero"),
            ]),
            new NullSaveFileDialogService());

        viewModel.SavePath = @"C:\fixture\ER0000.sl2";
        await viewModel.InitializeAsync();

        Assert.Collection(
            viewModel.SlotOptions,
            option =>
            {
                Assert.Equal(0, option.Value);
                Assert.Equal("スロット 1：Test Hero", option.Label);
            },
            option =>
            {
                Assert.Equal(2, option.Value);
                Assert.Equal("スロット 3：Second Hero", option.Label);
            });
        Assert.Equal(0, viewModel.SelectedSlotIndex);
        Assert.Equal("コレクション対象", viewModel.ModeOptions[0].Label);
        Assert.Equal("取得可能な全アイテム", viewModel.ModeOptions[1].Label);

        viewModel.Language = UiLanguage.English;

        Assert.Equal("Character", viewModel.SlotLabel);
        Assert.Equal("Slot 1: Test Hero", viewModel.SlotOptions[0].Label);
        Assert.Equal("Slot 3: Second Hero", viewModel.SlotOptions[1].Label);
        Assert.Equal("Missing", viewModel.StateFilterOptions[1].Label);
        Assert.Equal("Weapons", viewModel.CategoryOptions[1].Label);
        Assert.Equal("Collection", viewModel.ModeOptions[0].Label);
        Assert.Equal("Strict All Items", viewModel.ModeOptions[1].Label);
        Assert.Equal(0, viewModel.SelectedSlotIndex);
        Assert.Equal(SaveAnalysisStateFilter.Missing, viewModel.SelectedStateFilter);
        Assert.Equal(SaveAnalysisCategory.All, viewModel.SelectedCategory);
        Assert.Equal("Select a character and analyze.", viewModel.StatusMessage);
    }

    [Fact]
    public void SelectingAnItem_CopiesThePrimaryLocalizedName()
    {
        var clipboard = new StubClipboardService();
        var viewModel = new MainWindowViewModel(
            new StubSaveAnalysisService([]),
            new NullSaveFileDialogService(),
            new StubUserSettingsService(),
            clipboard);

        viewModel.SelectedItem = new ItemRowViewModel(
            Item() with { NameJa = "ロングソード", NameEn = "Longsword" },
            UiLanguage.Japanese,
            CollectionMode.Collection);

        Assert.Equal("ロングソード", clipboard.LastText);
        Assert.Equal("「ロングソード」をコピーしました", viewModel.CopyFeedbackText);

        clipboard.LastText = null;
        Assert.True(viewModel.CopySelectedItemNameCommand.CanExecute(null));
        viewModel.CopySelectedItemNameCommand.Execute(null);

        Assert.Equal("ロングソード", clipboard.LastText);
    }

    [Fact]
    public async Task SuccessfulCharacterLoading_PersistsTheSelectedSavePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ERCollectionCheckerJP-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var savePath = Path.Combine(directory, "ER0000.sl2");
            await File.WriteAllBytesAsync(savePath, []);
            var settings = new StubUserSettingsService(new UserSettings(savePath));
            var viewModel = new MainWindowViewModel(
                new StubSaveAnalysisService(
                [
                    new SaveSlotDescriptor(0, 261, false, "Test Hero"),
                ]),
                new NullSaveFileDialogService(),
                settings);

            Assert.Equal(savePath, viewModel.SavePath);

            await viewModel.InitializeAsync();

            Assert.Equal(savePath, settings.LastSaved?.SaveFilePath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static RuntimeDataPackPaths RuntimePaths() => RuntimeDataPackPaths.FromRoot(
        Path.Combine(AppContext.BaseDirectory, "data", "1.17", "runtime"));

    private static SaveAnalysisItem Item() => new(
        "weapon:1000000",
        "項目",
        "Item",
        1_000_000,
        ItemKind.Weapon,
        null,
        ContentPack.BaseGame,
        false,
        null,
        DetectionCoverage.Reviewed,
        CollectionScopeDisposition.Included,
        CollectionScopeDisposition.Included,
        CompletionState.Missing,
        [],
        null,
        null,
        null);

    private sealed class NullSaveFileDialogService : ISaveFileDialogService
    {
        public string? SelectSaveFile(string? currentPath, string title) => null;
    }

    private sealed class StubSaveAnalysisService(
        IReadOnlyList<SaveSlotDescriptor> slots) : ISaveAnalysisService
    {
        public Task<IReadOnlyList<SaveSlotDescriptor>> ReadSlotsAsync(
            string savePath,
            CancellationToken cancellationToken = default) => Task.FromResult(slots);

        public Task<SaveAnalysisResult> AnalyzeAsync(
            SaveAnalysisRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubUserSettingsService(
        UserSettings? settings = null) : IUserSettingsService
    {
        public UserSettings? LastSaved { get; private set; }

        public UserSettings Load() => settings ?? UserSettings.Default;

        public bool TrySave(UserSettings value)
        {
            LastSaved = value;
            return true;
        }
    }

    private sealed class StubClipboardService : IClipboardService
    {
        public string? LastText { get; set; }

        public bool TrySetText(string text)
        {
            LastText = text;
            return true;
        }
    }
}
