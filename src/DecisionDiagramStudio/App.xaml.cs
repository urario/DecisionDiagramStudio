using DecisionDiagramSharp;
using DecisionDiagramStudio.Services;
using DecisionDiagramStudio.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace DecisionDiagramStudio;

/// <summary>アプリケーションのエントリポイント。DI コンテナの構成と MainWindow の起動を担う。</summary>
public partial class App : Application
{
    /// <summary>アプリ全体で共有する DI サービスプロバイダー。OnLaunched 後に有効になる。</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    private Window? _window;

    /// <summary>App クラスを初期化する。</summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>アプリ起動時に DI コンテナを構成し MainWindow を表示する。</summary>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        _window = new MainWindow();
        _window.Activate();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(new DecisionDiagramOptions());
        services.AddSingleton<IDiagramService, DiagramService>();

        services.AddLogging(logging =>
        {
            logging.AddDebug();
            logging.SetMinimumLevel(LogLevel.Debug);
        });
    }
}
