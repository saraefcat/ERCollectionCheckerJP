using System.Windows;
using ERCollectionCheckerJP.Application.Analysis;
using ERCollectionCheckerJP.App.Services;

namespace ERCollectionCheckerJP.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            await using var runtimeData = await EmbeddedRuntimeData.ExtractAsync();
            var runtimeCatalog = await RuntimeCatalogLoader.LoadAsync(
                RuntimeDataPackPaths.FromRoot(runtimeData.RootDirectory));
            var window = new MainWindow(new SaveAnalysisService(runtimeCatalog));
            MainWindow = window;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            window.Show();
        }
        catch (CollectionCheckException exception)
        {
            MessageBox.Show(
                UserMessage(exception.Code),
                "ER Collection Checker JP",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
        catch (Exception)
        {
            MessageBox.Show(
                "アプリケーションの初期化中に予期しないエラーが発生しました。",
                "ER Collection Checker JP",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static string UserMessage(CollectionCheckErrorCode code) => code switch
    {
        CollectionCheckErrorCode.DatabaseNotFound =>
            "必要な1.17用データファイルが見つかりません。アプリケーションを再配置してください。",
        CollectionCheckErrorCode.DatabaseVersionMismatch =>
            "1.17用データファイルが破損しているか、対応していない組み合わせです。",
        CollectionCheckErrorCode.AccessDenied =>
            "1.17用データファイルを読み取る権限がありません。",
        CollectionCheckErrorCode.InvalidConfiguration =>
            "アプリケーションのデータファイル設定が不正です。",
        _ => "アプリケーションを初期化できませんでした。",
    };
}
