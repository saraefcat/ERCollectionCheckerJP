using System.Globalization;
using System.IO;
using System.Windows.Input;
using ERCollectionCheckerJP.App.Presentation;
using ERCollectionCheckerJP.App.Services;
using ERCollectionCheckerJP.Application.Analysis;
using ERCollectionCheckerJP.Application.Completion;
using ERCollectionCheckerJP.Domain.Items;

namespace ERCollectionCheckerJP.App.ViewModels;

public enum UiLanguage
{
    Japanese,
    English,
}

public sealed record SelectionOption<T>(T Value, string Label);

public sealed record SummaryCardViewModel(
    string Title,
    string CountText,
    string PercentText,
    string Accent);

public sealed class ItemRowViewModel
{
    public ItemRowViewModel(
        SaveAnalysisItem source,
        UiLanguage language,
        CollectionMode mode)
    {
        Source = source;
        PrimaryName = DisplayName(source, language);
        SecondaryName = DisplayName(
            source,
            language is UiLanguage.Japanese ? UiLanguage.English : UiLanguage.Japanese);
        StateText = StateLabel(source, language);
        StateBrush = source.State switch
        {
            CompletionState.Owned when
                source.ArmorState is ArmorCollectionState.CoveredByConversion => "#DDBB72",
            CompletionState.Owned => "#6FD39A",
            CompletionState.Missing => "#F07C82",
            CompletionState.Unknown => "#F0C36A",
            CompletionState.Excluded => "#8793A1",
            _ => "#8793A1",
        };
        CategoryText = CategoryLabel(SaveAnalysisViewQuery.GetCategory(source), language);
        ContentPackText = ContentPackLabel(source.ContentPack, language);
        ParamIdText = source.ParamId.ToString(CultureInfo.InvariantCulture);
        DetailText = DetailLabel(source, language);
        ScopeText = ScopeLabel(source, language, mode);
        ExclusionText = source.ExclusionReason is ExclusionReason reason
            ? ExclusionLabel(reason, language)
            : "—";
    }

    public SaveAnalysisItem Source { get; }

    public string PrimaryName { get; }

    public string SecondaryName { get; }

    public string StateText { get; }

    public string StateBrush { get; }

    public string CategoryText { get; }

    public string ContentPackText { get; }

    public string ParamIdText { get; }

    public string DetailText { get; }

    public string ScopeText { get; }

    public string ExclusionText { get; }

    private static string DisplayName(SaveAnalysisItem item, UiLanguage language)
    {
        var primary = language is UiLanguage.Japanese ? item.NameJa : item.NameEn;
        var fallback = language is UiLanguage.Japanese ? item.NameEn : item.NameJa;
        return primary ?? fallback ?? item.Key;
    }

    private static string StateLabel(SaveAnalysisItem item, UiLanguage language)
    {
        if (item.ArmorState is ArmorCollectionState.CoveredByConversion)
        {
            return Text(language, "変換可能", "Convertible");
        }

        return item.State switch
        {
            CompletionState.Owned => Text(language, "所持済み", "Owned"),
            CompletionState.Missing => Text(language, "未所持", "Missing"),
            CompletionState.Unknown => Text(language, "未判定", "Unknown"),
            CompletionState.Excluded => Text(language, "対象外", "Excluded"),
            _ => Text(language, "未判定", "Unknown"),
        };
    }

    private static string DetailLabel(SaveAnalysisItem item, UiLanguage language)
    {
        if (item.IsDataOnly)
        {
            return Text(language, "ゲームデータ上のみ存在", "Data-only item");
        }

        if (item.ArmorState is ArmorCollectionState.CoveredByConversion)
        {
            return item.ConversionSourceItemKey is string source
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    Text(language, "{0} から変換可能", "Convertible from {0}"),
                    source)
                : Text(language, "別形態から変換可能", "Convertible from another form");
        }

        if (item.UnknownReasons.Count > 0)
        {
            return string.Join(", ", item.UnknownReasons);
        }

        return item.State switch
        {
            CompletionState.Owned => Text(language, "検証済みの所持根拠あり", "Verified ownership evidence"),
            CompletionState.Missing => Text(language, "検証済みの所持根拠なし", "No verified ownership evidence"),
            _ => "—",
        };
    }

    private static string ScopeLabel(
        SaveAnalysisItem item,
        UiLanguage language,
        CollectionMode mode)
    {
        if (item.IsDataOnly)
        {
            return Text(language, "常に進捗対象外", "Always excluded from progress");
        }

        var disposition = mode is CollectionMode.Collection
            ? item.CollectionScope
            : item.StrictScope;
        return disposition switch
        {
            CollectionScopeDisposition.Included => mode is CollectionMode.Collection
                ? Text(language, "Collection対象", "Included in Collection")
                : Text(language, "Strict対象", "Included in Strict"),
            CollectionScopeDisposition.Unreviewed => mode is CollectionMode.Collection
                ? Text(language, "Collection分類保留", "Collection classification pending")
                : Text(language, "Strict分類保留", "Strict classification pending"),
            CollectionScopeDisposition.Excluded => mode is CollectionMode.Collection
                ? Text(language, "Collection対象外", "Excluded from Collection")
                : Text(language, "Strict対象外", "Excluded from Strict"),
            _ => "—",
        };
    }

    internal static string CategoryLabel(SaveAnalysisCategory category, UiLanguage language) => category switch
    {
        SaveAnalysisCategory.All => Text(language, "すべてのカテゴリ", "All categories"),
        SaveAnalysisCategory.Weapon => Text(language, "武器", "Weapons"),
        SaveAnalysisCategory.Armor => Text(language, "防具", "Armor"),
        SaveAnalysisCategory.Accessory => Text(language, "タリスマン", "Talismans"),
        SaveAnalysisCategory.AshOfWar => Text(language, "戦灰", "Ashes of War"),
        SaveAnalysisCategory.Sorcery => Text(language, "魔術", "Sorceries"),
        SaveAnalysisCategory.Incantation => Text(language, "祈祷", "Incantations"),
        SaveAnalysisCategory.Gesture => Text(language, "ジェスチャー", "Gestures"),
        SaveAnalysisCategory.TorrentAttire => Text(language, "霊馬装束", "Torrent Attire"),
        SaveAnalysisCategory.SpiritAsh => Text(language, "遺灰", "Spirit Ashes"),
        SaveAnalysisCategory.CrystalTear => Text(language, "結晶雫", "Crystal Tears"),
        SaveAnalysisCategory.OtherGoods => Text(language, "その他の道具", "Other Goods"),
        _ => Text(language, "その他", "Other"),
    };

    private static string ContentPackLabel(ContentPack pack, UiLanguage language) => pack switch
    {
        ContentPack.BaseGame => Text(language, "本編", "Base Game"),
        ContentPack.ShadowOfTheErdtree => Text(language, "SHADOW OF THE ERDTREE", "SHADOW OF THE ERDTREE"),
        ContentPack.TarnishedPack => Text(language, "Tarnished Pack", "Tarnished Pack"),
        _ => Text(language, "不明", "Unknown"),
    };

    private static string ExclusionLabel(ExclusionReason reason, UiLanguage language) => reason switch
    {
        ExclusionReason.Unobtainable => Text(language, "通常入手不能", "Unobtainable"),
        ExclusionReason.CutContent => Text(language, "未使用データ", "Cut content"),
        ExclusionReason.DeveloperTest => Text(language, "開発用", "Developer test"),
        ExclusionReason.NpcOnly => Text(language, "NPC専用", "NPC only"),
        ExclusionReason.Placeholder => Text(language, "プレースホルダー", "Placeholder"),
        ExclusionReason.InternalDummy => Text(language, "内部ダミー", "Internal dummy"),
        ExclusionReason.ModOnly => Text(language, "MODのみ", "Mod only"),
        _ => Text(language, "その他", "Other"),
    };

    private static string Text(UiLanguage language, string japanese, string english) =>
        language is UiLanguage.Japanese ? japanese : english;
}

public sealed class MainWindowViewModel : ObservableObject
{
    private enum StatusKind
    {
        Ready,
        LoadingSlots,
        SlotsLoaded,
        NoCharacters,
        Loading,
        Loaded,
        EmptySlot,
        SelectionChanged,
        Error,
        UnexpectedError,
    }

    private readonly ISaveAnalysisService analysisService;
    private readonly ISaveFileDialogService fileDialogService;
    private readonly IUserSettingsService userSettingsService;
    private readonly IClipboardService clipboardService;
    private readonly AsyncRelayCommand browseCommand;
    private readonly AsyncRelayCommand analyzeCommand;
    private readonly RelayCommand copySelectedItemNameCommand;
    private SaveAnalysisResult? currentResult;
    private string savePath = string.Empty;
    private int selectedSlotIndex;
    private UiLanguage language = UiLanguage.Japanese;
    private CollectionMode selectedMode = CollectionMode.Collection;
    private SaveAnalysisStateFilter selectedStateFilter = SaveAnalysisStateFilter.Missing;
    private SaveAnalysisCategory selectedCategory = SaveAnalysisCategory.All;
    private string searchText = string.Empty;
    private bool includeBaseGame = true;
    private bool includeShadowOfTheErdtree = true;
    private bool includeTarnishedPack = true;
    private bool developerMode;
    private bool showDataOnlyItems;
    private bool isBusy;
    private ItemRowViewModel? selectedItem;
    private IReadOnlyList<ItemRowViewModel> items = [];
    private IReadOnlyList<SummaryCardViewModel> categorySummaries = [];
    private IReadOnlyList<SaveSlotDescriptor> availableSlots = [];
    private IReadOnlyList<SelectionOption<int>> slotOptions = [];
    private SaveAnalysisProgress? progress;
    private DateTimeOffset? lastScan;
    private string? copyFeedbackText;
    private StatusKind status = StatusKind.Ready;
    private CollectionCheckErrorCode? lastError;

    public MainWindowViewModel(
        ISaveAnalysisService analysisService,
        ISaveFileDialogService fileDialogService,
        IUserSettingsService? userSettingsService = null,
        IClipboardService? clipboardService = null)
    {
        ArgumentNullException.ThrowIfNull(analysisService);
        ArgumentNullException.ThrowIfNull(fileDialogService);
        this.analysisService = analysisService;
        this.fileDialogService = fileDialogService;
        this.userSettingsService = userSettingsService ?? NullUserSettingsService.Instance;
        this.clipboardService = clipboardService ?? NullClipboardService.Instance;
        browseCommand = new AsyncRelayCommand(BrowseAsync, () => !IsBusy);
        analyzeCommand = new AsyncRelayCommand(
            AnalyzeAsync,
            () => !IsBusy && SavePath.Length > 0 && SlotOptions.Count > 0);
        copySelectedItemNameCommand = new RelayCommand(
            CopySelectedItemName,
            () => SelectedItem is not null);
        ClearFiltersCommand = new RelayCommand(ClearFilters);
        var savedPath = this.userSettingsService.Load().SaveFilePath;
        savePath = savedPath is not null && File.Exists(savedPath)
            ? savedPath
            : TryDetectSingleSave() ?? string.Empty;
    }

    public ICommand BrowseCommand => browseCommand;

    public ICommand AnalyzeCommand => analyzeCommand;

    public ICommand CopySelectedItemNameCommand => copySelectedItemNameCommand;

    public ICommand ClearFiltersCommand { get; }

    public IReadOnlyList<SelectionOption<UiLanguage>> LanguageOptions =>
    [
        new(UiLanguage.Japanese, "日本語"),
        new(UiLanguage.English, "English"),
    ];

    public IReadOnlyList<SelectionOption<int>> SlotOptions
    {
        get => slotOptions;
        private set
        {
            if (SetProperty(ref slotOptions, value))
            {
                analyzeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public IReadOnlyList<SelectionOption<CollectionMode>> ModeOptions =>
    [
        new(CollectionMode.Collection, Text("コレクション対象", "Collection")),
        new(CollectionMode.StrictAllItems, Text("取得可能な全アイテム", "Strict All Items")),
    ];

    public IReadOnlyList<SelectionOption<SaveAnalysisStateFilter>> StateFilterOptions =>
    [
        new(SaveAnalysisStateFilter.All, Text("すべて", "All")),
        new(SaveAnalysisStateFilter.Missing, Text("未所持", "Missing")),
        new(SaveAnalysisStateFilter.Owned, Text("所持済み", "Owned")),
        new(SaveAnalysisStateFilter.Unknown, Text("未判定", "Unknown")),
        new(SaveAnalysisStateFilter.Excluded, Text("対象外", "Excluded")),
    ];

    public IReadOnlyList<SelectionOption<SaveAnalysisCategory>> CategoryOptions =>
        Enum.GetValues<SaveAnalysisCategory>()
            .Select(category => new SelectionOption<SaveAnalysisCategory>(
                category,
                ItemRowViewModel.CategoryLabel(category, Language)))
            .ToArray();

    public string SavePath
    {
        get => savePath;
        set
        {
            if (SetProperty(ref savePath, value ?? string.Empty))
            {
                availableSlots = [];
                SlotOptions = [];
                SelectionChanged();
                analyzeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public int SelectedSlotIndex
    {
        get => selectedSlotIndex;
        set
        {
            if (SetProperty(ref selectedSlotIndex, value))
            {
                SelectionChanged();
            }
        }
    }

    public UiLanguage Language
    {
        get => language;
        set
        {
            if (SetProperty(ref language, value))
            {
                copyFeedbackText = null;
                RebuildSlotOptions();
                RaiseLocalizedProperties();
                RefreshPresentation();
            }
        }
    }

    public CollectionMode SelectedMode
    {
        get => selectedMode;
        set
        {
            if (SetProperty(ref selectedMode, value))
            {
                RefreshPresentation();
            }
        }
    }

    public SaveAnalysisStateFilter SelectedStateFilter
    {
        get => selectedStateFilter;
        set
        {
            if (SetProperty(ref selectedStateFilter, value))
            {
                RefreshPresentation();
            }
        }
    }

    public SaveAnalysisCategory SelectedCategory
    {
        get => selectedCategory;
        set
        {
            if (SetProperty(ref selectedCategory, value))
            {
                RefreshPresentation();
            }
        }
    }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetProperty(ref searchText, value ?? string.Empty))
            {
                RefreshPresentation();
            }
        }
    }

    public bool IncludeBaseGame
    {
        get => includeBaseGame;
        set
        {
            if (SetProperty(ref includeBaseGame, value))
            {
                RefreshPresentation();
            }
        }
    }

    public bool IncludeShadowOfTheErdtree
    {
        get => includeShadowOfTheErdtree;
        set
        {
            if (SetProperty(ref includeShadowOfTheErdtree, value))
            {
                RefreshPresentation();
            }
        }
    }

    public bool IncludeTarnishedPack
    {
        get => includeTarnishedPack;
        set
        {
            if (SetProperty(ref includeTarnishedPack, value))
            {
                RefreshPresentation();
            }
        }
    }

    public bool DeveloperMode
    {
        get => developerMode;
        set
        {
            if (SetProperty(ref developerMode, value))
            {
                if (!value)
                {
                    showDataOnlyItems = false;
                    OnPropertyChanged(nameof(ShowDataOnlyItems));
                    RestoreDefaultStateFilterAfterDataOnly();
                }

                OnPropertyChanged(nameof(IsDataOnlyToggleEnabled));
                RefreshPresentation();
            }
        }
    }

    public bool ShowDataOnlyItems
    {
        get => showDataOnlyItems;
        set
        {
            if (value && !DeveloperMode)
            {
                return;
            }

            if (SetProperty(ref showDataOnlyItems, value))
            {
                if (value && selectedStateFilter is not SaveAnalysisStateFilter.Excluded)
                {
                    selectedStateFilter = SaveAnalysisStateFilter.Excluded;
                    OnPropertyChanged(nameof(SelectedStateFilter));
                }
                else if (!value)
                {
                    RestoreDefaultStateFilterAfterDataOnly();
                }

                RefreshPresentation();
            }
        }
    }

    public bool IsDataOnlyToggleEnabled => DeveloperMode;

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value))
            {
                browseCommand.NotifyCanExecuteChanged();
                analyzeCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(CanEditSelection));
            }
        }
    }

    public bool CanEditSelection => !IsBusy;

    public bool HasAvailableCharacters => SlotOptions.Count > 0;

    public IReadOnlyList<ItemRowViewModel> Items
    {
        get => items;
        private set => SetProperty(ref items, value);
    }

    public IReadOnlyList<SummaryCardViewModel> CategorySummaries
    {
        get => categorySummaries;
        private set => SetProperty(ref categorySummaries, value);
    }

    public ItemRowViewModel? SelectedItem
    {
        get => selectedItem;
        set
        {
            if (SetProperty(ref selectedItem, value))
            {
                copySelectedItemNameCommand.NotifyCanExecuteChanged();
                if (value is not null)
                {
                    CopySelectedItemName();
                }
            }
        }
    }

    public string CopyFeedbackText => copyFeedbackText ?? SortHintText;

    public bool HasResult => currentResult is not null;

    public double ProgressValue => (double)(progress?.CompletionPercent ?? 0);

    public string ProgressPercentText => progress?.CompletionPercent is decimal percent
        ? $"{percent:0.00}%"
        : "—";

    public string ProgressCountText => progress is null
        ? "—"
        : $"{progress.OwnedItemCount:N0} / {progress.CompletionDenominator:N0}";

    public string CoverageText => progress is null
        ? "—"
        : string.Format(
            CultureInfo.CurrentCulture,
            Text("判定完了 {0:N0} / {1:N0}  ({2})", "Decided {0:N0} / {1:N0}  ({2})"),
            progress.CompletionDenominator,
            progress.IncludedItemCount,
            progress.DecisionCoveragePercent is decimal coverage ? $"{coverage:0.00}%" : "—");

    public string SummaryDetailText => progress is null
        ? Text("セーブを選択して解析してください", "Select and analyze a save file")
        : string.Format(
            CultureInfo.CurrentCulture,
            Text(
                "所持 {0:N0} ・ 未所持 {1:N0} ・ 未判定 {2:N0} ・ 分類保留 {3:N0}",
                "Owned {0:N0} · Missing {1:N0} · Unknown {2:N0} · Pending {3:N0}"),
            progress.OwnedItemCount,
            progress.MissingItemCount,
            progress.UnknownItemCount,
            progress.UnreviewedItemCount);

    public string VisibleItemsText => string.Format(
        CultureInfo.CurrentCulture,
        Text("表示 {0:N0} 件", "Showing {0:N0} items"),
        Items.Count);

    public string DatabaseText => currentResult is null
        ? "1.17"
        : currentResult.DataPackVersions.ReviewedItemDatabase;

    public string LastScanText => lastScan is DateTimeOffset value
        ? value.ToLocalTime().ToString(
            Language is UiLanguage.Japanese ? "yyyy/MM/dd HH:mm:ss" : "yyyy-MM-dd HH:mm:ss",
            CultureInfo.CurrentCulture)
        : "—";

    public string ResultSourceText => currentResult is null
        ? "—"
        : string.Format(
            CultureInfo.CurrentCulture,
            "{0} / {1}",
            currentResult.Save.FileName,
            SlotDisplayName(currentResult.SlotIndex));

    public string StatusMessage => status switch
    {
        StatusKind.Ready => SavePath.Length == 0
            ? Text("セーブデータを選択してください。", "Select a save file.")
            : Text("キャラクターを読み込んでいません。", "Characters have not been loaded."),
        StatusKind.LoadingSlots => Text(
            "セーブデータからキャラクターを読み込んでいます…",
            "Loading characters from the save…"),
        StatusKind.SlotsLoaded => Text(
            "キャラクターを選択して解析できます。",
            "Select a character and analyze."),
        StatusKind.NoCharacters => Text(
            "使用中のキャラクターが見つかりません。",
            "No occupied character slots were found."),
        StatusKind.Loading => Text("読み取り専用で解析しています…", "Analyzing read-only snapshot…"),
        StatusKind.Loaded => Text("解析が完了しました。", "Analysis completed."),
        StatusKind.EmptySlot => Text("選択したスロットは空です。", "The selected slot is empty."),
        StatusKind.SelectionChanged => Text(
            "選択が変わりました。解析を実行してください。前回結果はそのまま表示しています。",
            "Selection changed. Analyze again; the previous result remains visible."),
        StatusKind.Error => ErrorMessage(lastError),
        StatusKind.UnexpectedError => Text(
            "予期しないエラーが発生しました。前回の結果を保持しています。",
            "An unexpected error occurred. The previous result is still displayed."),
        _ => string.Empty,
    };

    public string WindowTitle => Text("ER Collection Checker JP", "ER Collection Checker JP");

    public string ReadOnlyLabel => Text("読み取り専用", "READ ONLY");

    public string SaveLabel => Text("セーブデータ", "Save file");

    public string BrowseLabel => Text("ファイルを選択", "Select file");

    public string AnalyzeLabel => IsBusy && status is StatusKind.Loading
        ? Text("解析中…", "Analyzing…")
        : Text("解析", "Analyze");

    public string SlotLabel => Text("キャラクター", "Character");

    public string LanguageLabel => Text("言語", "Language");

    public string ModeLabel => Text("収集モード", "Collection mode");

    public string GameLabel => Text("ゲーム", "Game");

    public string DatabaseLabel => Text("データベース", "Database");

    public string LastScanLabel => Text("最終解析", "Last scan");

    public string ResultLabel => Text("表示中の結果", "Displayed result");

    public string OverallProgressLabel => Text("全体進捗", "Overall progress");

    public string CategoryProgressLabel => Text("カテゴリ別", "By category");

    public string SearchLabel => Text("検索（日本語・英語）", "Search (Japanese / English)");

    public string StateLabel => Text("状態", "State");

    public string CategoryLabel => Text("カテゴリ", "Category");

    public string ContentPackLabel => Text("コンテンツ", "Content packs");

    public string BaseGameLabel => Text("本編", "Base Game");

    public string ShadowLabel => "SHADOW OF THE ERDTREE";

    public string TarnishedLabel => "Tarnished Pack";

    public string DeveloperModeLabel => Text("開発者モード", "Developer mode");

    public string DataOnlyLabel => Text("DataOnlyを表示", "Show DataOnly");

    public string ClearFiltersLabel => Text("フィルターをリセット", "Reset filters");

    public string ItemListLabel => Text("収集アイテム", "Collection items");

    public string SortHintText => Text(
        "レコード選択または Ctrl+C で名称をコピー・列見出しで並べ替え",
        "Select a row or press Ctrl+C to copy its name · Click a column header to sort");

    public string CopyNameLabel => Text("名称をコピー", "Copy name");

    public string DetailLabel => Text("アイテム詳細", "Item details");

    public string SelectItemLabel => Text(
        "一覧からアイテムを選択してください。",
        "Select an item from the list.");

    public string NameColumnLabel => Text("名称", "Name");

    public string ContentColumnLabel => Text("DLC / Pack", "DLC / Pack");

    public string NoteColumnLabel => Text("判定メモ", "Decision note");

    public string ParamIdLabel => "Param ID";

    public string ScopeLabel => Text("対象範囲", "Scope");

    public string ExclusionLabel => Text("除外理由", "Exclusion reason");

    public async Task InitializeAsync()
    {
        if (SavePath.Length > 0 && SlotOptions.Count == 0 && !IsBusy)
        {
            await LoadSlotsAsync();
        }
    }

    private async Task BrowseAsync()
    {
        var selected = fileDialogService.SelectSaveFile(
            SavePath,
            Text("ELDEN RING セーブデータを選択", "Select an ELDEN RING save file"));
        if (selected is not null)
        {
            SavePath = selected;
            await LoadSlotsAsync();
        }
    }

    private async Task LoadSlotsAsync()
    {
        IsBusy = true;
        status = StatusKind.LoadingSlots;
        lastError = null;
        OnPropertyChanged(nameof(StatusMessage));
        try
        {
            var slots = await Task.Run(() => analysisService.ReadSlotsAsync(SavePath));
            availableSlots = slots.Where(static slot => !slot.IsEmpty).ToArray();
            RebuildSlotOptions();
            if (availableSlots.Count == 0)
            {
                status = StatusKind.NoCharacters;
            }
            else
            {
                if (!availableSlots.Any(slot => slot.SlotIndex == SelectedSlotIndex))
                {
                    SelectedSlotIndex = availableSlots[0].SlotIndex;
                }

                status = StatusKind.SlotsLoaded;
            }

            _ = userSettingsService.TrySave(new UserSettings(SavePath));
        }
        catch (CollectionCheckException exception)
        {
            availableSlots = [];
            SlotOptions = [];
            lastError = exception.Code;
            status = StatusKind.Error;
        }
        catch (Exception)
        {
            availableSlots = [];
            SlotOptions = [];
            status = StatusKind.UnexpectedError;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(HasAvailableCharacters));
            OnPropertyChanged(nameof(StatusMessage));
        }
    }

    private async Task AnalyzeAsync()
    {
        IsBusy = true;
        status = StatusKind.Loading;
        lastError = null;
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(AnalyzeLabel));
        try
        {
            var request = new SaveAnalysisRequest(SavePath, SelectedSlotIndex);
            var result = await Task.Run(() => analysisService.AnalyzeAsync(request));
            currentResult = result;
            lastScan = DateTimeOffset.Now;
            status = result.IsEmptySlot ? StatusKind.EmptySlot : StatusKind.Loaded;
            RefreshPresentation();
            OnPropertyChanged(nameof(HasResult));
            OnPropertyChanged(nameof(DatabaseText));
            OnPropertyChanged(nameof(LastScanText));
            OnPropertyChanged(nameof(ResultSourceText));
        }
        catch (CollectionCheckException exception)
        {
            lastError = exception.Code;
            status = StatusKind.Error;
        }
        catch (Exception)
        {
            status = StatusKind.UnexpectedError;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(StatusMessage));
            OnPropertyChanged(nameof(AnalyzeLabel));
        }
    }

    private void ClearFilters()
    {
        ShowDataOnlyItems = false;
        DeveloperMode = false;
        SearchText = string.Empty;
        SelectedStateFilter = SaveAnalysisStateFilter.Missing;
        SelectedCategory = SaveAnalysisCategory.All;
        IncludeBaseGame = true;
        IncludeShadowOfTheErdtree = true;
        IncludeTarnishedPack = true;
    }

    private void CopySelectedItemName()
    {
        if (SelectedItem is not ItemRowViewModel item)
        {
            return;
        }

        copyFeedbackText = clipboardService.TrySetText(item.PrimaryName)
            ? string.Format(
                CultureInfo.CurrentCulture,
                Text("「{0}」をコピーしました", "Copied “{0}”"),
                item.PrimaryName)
            : Text("名称をコピーできませんでした", "Could not copy the name");
        OnPropertyChanged(nameof(CopyFeedbackText));
    }

    private void SelectionChanged()
    {
        if (currentResult is not null)
        {
            status = StatusKind.SelectionChanged;
            OnPropertyChanged(nameof(StatusMessage));
        }
    }

    private void RefreshPresentation()
    {
        if (currentResult is null)
        {
            Items = [];
            CategorySummaries = [];
            progress = null;
        }
        else
        {
            var contentPacks = IncludedContentPacks();
            var options = new SaveAnalysisFilterOptions(
                SelectedMode,
                contentPacks,
                SelectedStateFilter,
                SelectedCategory,
                SearchText,
                DeveloperMode,
                ShowDataOnlyItems);
            var selectedKey = SelectedItem?.Source.Key;
            Items = SaveAnalysisViewQuery.Filter(currentResult.Items, options)
                .Select(item => new ItemRowViewModel(item, Language, SelectedMode))
                .ToArray();
            SelectedItem = selectedKey is null
                ? null
                : Items.FirstOrDefault(item =>
                    string.Equals(item.Source.Key, selectedKey, StringComparison.Ordinal));
            progress = SaveAnalysisViewQuery.BuildProgress(
                currentResult.Items,
                SelectedMode,
                contentPacks);
            CategorySummaries = SaveAnalysisViewQuery.BuildCategoryProgress(
                    currentResult.Items,
                    SelectedMode,
                    contentPacks)
                .Select(category => new SummaryCardViewModel(
                    ItemRowViewModel.CategoryLabel(category.Category, Language),
                    $"{category.OwnedItemCount:N0} / {category.OwnedItemCount + category.MissingItemCount:N0}",
                    category.CompletionPercent is decimal percent ? $"{percent:0.00}%" : "—",
                    category.UnknownItemCount == 0 ? "#6FD39A" : "#F0C36A"))
                .ToArray();
        }

        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(ProgressValue));
        OnPropertyChanged(nameof(ProgressPercentText));
        OnPropertyChanged(nameof(ProgressCountText));
        OnPropertyChanged(nameof(CoverageText));
        OnPropertyChanged(nameof(SummaryDetailText));
        OnPropertyChanged(nameof(VisibleItemsText));
    }

    private IReadOnlySet<ContentPack> IncludedContentPacks()
    {
        var result = new HashSet<ContentPack>();
        if (IncludeBaseGame)
        {
            result.Add(ContentPack.BaseGame);
        }

        if (IncludeShadowOfTheErdtree)
        {
            result.Add(ContentPack.ShadowOfTheErdtree);
        }

        if (IncludeTarnishedPack)
        {
            result.Add(ContentPack.TarnishedPack);
        }

        return result;
    }

    private void RestoreDefaultStateFilterAfterDataOnly()
    {
        if (selectedStateFilter is SaveAnalysisStateFilter.Excluded)
        {
            selectedStateFilter = SaveAnalysisStateFilter.Missing;
            OnPropertyChanged(nameof(SelectedStateFilter));
        }
    }

    private void RebuildSlotOptions()
    {
        SlotOptions = availableSlots
            .Select(slot => new SelectionOption<int>(
                slot.SlotIndex,
                FormatSlot(slot.SlotIndex, slot.CharacterName)))
            .ToArray();
        OnPropertyChanged(nameof(HasAvailableCharacters));
        OnPropertyChanged(nameof(SelectedSlotIndex));
        OnPropertyChanged(nameof(ResultSourceText));
    }

    private string SlotDisplayName(int slotIndex)
    {
        var slot = availableSlots.FirstOrDefault(candidate => candidate.SlotIndex == slotIndex);
        return FormatSlot(slotIndex, slot?.CharacterName);
    }

    private string FormatSlot(int slotIndex, string? characterName) => string.Format(
        CultureInfo.CurrentCulture,
        Text("スロット {0}：{1}", "Slot {0}: {1}"),
        slotIndex + 1,
        characterName ?? Text("名前不明", "Unknown name"));

    private void RaiseLocalizedProperties()
    {
        OnPropertyChanged(nameof(StateFilterOptions));
        OnPropertyChanged(nameof(SelectedStateFilter));
        OnPropertyChanged(nameof(CategoryOptions));
        OnPropertyChanged(nameof(SelectedCategory));
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(ModeOptions));
        OnPropertyChanged(nameof(SelectedMode));
        OnPropertyChanged(nameof(LastScanText));
        OnPropertyChanged(nameof(ResultSourceText));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(ReadOnlyLabel));
        OnPropertyChanged(nameof(SaveLabel));
        OnPropertyChanged(nameof(BrowseLabel));
        OnPropertyChanged(nameof(AnalyzeLabel));
        OnPropertyChanged(nameof(SlotLabel));
        OnPropertyChanged(nameof(LanguageLabel));
        OnPropertyChanged(nameof(ModeLabel));
        OnPropertyChanged(nameof(GameLabel));
        OnPropertyChanged(nameof(DatabaseLabel));
        OnPropertyChanged(nameof(LastScanLabel));
        OnPropertyChanged(nameof(ResultLabel));
        OnPropertyChanged(nameof(OverallProgressLabel));
        OnPropertyChanged(nameof(CategoryProgressLabel));
        OnPropertyChanged(nameof(SearchLabel));
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(CategoryLabel));
        OnPropertyChanged(nameof(ContentPackLabel));
        OnPropertyChanged(nameof(BaseGameLabel));
        OnPropertyChanged(nameof(DeveloperModeLabel));
        OnPropertyChanged(nameof(DataOnlyLabel));
        OnPropertyChanged(nameof(ClearFiltersLabel));
        OnPropertyChanged(nameof(ItemListLabel));
        OnPropertyChanged(nameof(SortHintText));
        OnPropertyChanged(nameof(CopyNameLabel));
        OnPropertyChanged(nameof(CopyFeedbackText));
        OnPropertyChanged(nameof(DetailLabel));
        OnPropertyChanged(nameof(SelectItemLabel));
        OnPropertyChanged(nameof(NameColumnLabel));
        OnPropertyChanged(nameof(ContentColumnLabel));
        OnPropertyChanged(nameof(NoteColumnLabel));
        OnPropertyChanged(nameof(ScopeLabel));
        OnPropertyChanged(nameof(ExclusionLabel));
        OnPropertyChanged(nameof(CoverageText));
        OnPropertyChanged(nameof(SummaryDetailText));
        OnPropertyChanged(nameof(VisibleItemsText));
    }

    private string ErrorMessage(CollectionCheckErrorCode? code) => code switch
    {
        CollectionCheckErrorCode.InvalidConfiguration => Text(
            "セーブデータのパスまたは設定が正しくありません。",
            "The save path or configuration is invalid."),
        CollectionCheckErrorCode.InvalidSlot => Text(
            "選択したキャラクタースロットは範囲外です。",
            "The selected character slot is out of range."),
        CollectionCheckErrorCode.SaveNotFound => Text(
            "セーブデータが見つかりません。前回の結果を保持しています。",
            "The save file was not found. The previous result is still displayed."),
        CollectionCheckErrorCode.AccessDenied => Text(
            "セーブデータを読み取る権限がありません。",
            "Access to the save file was denied."),
        CollectionCheckErrorCode.SaveChangedWhileReading => Text(
            "保存中にデータが変化しました。保存完了後に再解析してください。",
            "The save changed during analysis. Try again after saving finishes."),
        CollectionCheckErrorCode.SaveReadFailed => Text(
            "セーブデータを読み取れませんでした。前回の結果を保持しています。",
            "The save could not be read. The previous result is still displayed."),
        CollectionCheckErrorCode.InvalidSave => Text(
            "セーブデータが破損しているか、未対応の形式です。",
            "The save is damaged or uses an unsupported format."),
        CollectionCheckErrorCode.DatabaseNotFound => Text(
            "必要な1.17用データベースが見つかりません。",
            "The required 1.17 database was not found."),
        CollectionCheckErrorCode.DatabaseVersionMismatch => Text(
            "1.17用データベースの組み合わせが正しくありません。",
            "The 1.17 database files do not match."),
        _ => Text(
            "解析に失敗しました。前回の結果を保持しています。",
            "Analysis failed. The previous result is still displayed."),
    };

    private string Text(string japanese, string english) =>
        Language is UiLanguage.Japanese ? japanese : english;

    private static string? TryDetectSingleSave()
    {
        try
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EldenRing");
            if (!Directory.Exists(root))
            {
                return null;
            }

            var matches = Directory.EnumerateFiles(root, "ER0000.sl2", SearchOption.AllDirectories)
                .Take(2)
                .ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
