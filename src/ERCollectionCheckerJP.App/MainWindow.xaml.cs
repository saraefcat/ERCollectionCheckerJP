using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ERCollectionCheckerJP.App.Services;
using ERCollectionCheckerJP.App.ViewModels;
using ERCollectionCheckerJP.Application.Analysis;

namespace ERCollectionCheckerJP.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel viewModel;

    public MainWindow(
        ISaveAnalysisService analysisService,
        IUserSettingsService? userSettingsService = null,
        IClipboardService? clipboardService = null)
    {
        ArgumentNullException.ThrowIfNull(analysisService);
        InitializeComponent();
        viewModel = new MainWindowViewModel(
            analysisService,
            new SaveFileDialogService(),
            userSettingsService ?? new JsonUserSettingsService(),
            clipboardService ?? new ClipboardService());
        viewModel.PropertyChanged += ViewModelOnPropertyChanged;
        DataContext = viewModel;
        UpdateDeveloperVisibility();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await viewModel.InitializeAsync();
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(MainWindowViewModel.DeveloperMode), StringComparison.Ordinal))
        {
            UpdateDeveloperVisibility();
        }
    }

    private void UpdateDeveloperVisibility()
    {
        var visibility = viewModel.DeveloperMode ? Visibility.Visible : Visibility.Collapsed;
        NoteColumn.Visibility = visibility;
        ItemDetailsPanel.Visibility = visibility;
        DetailColumn.Width = viewModel.DeveloperMode
            ? new GridLength(285)
            : new GridLength(0);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        viewModel.PropertyChanged -= ViewModelOnPropertyChanged;
        Loaded -= OnLoaded;
        Closed -= OnClosed;
    }
}
