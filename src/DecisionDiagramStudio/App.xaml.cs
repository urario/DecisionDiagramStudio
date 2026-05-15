using DecisionDiagramSharp;
using DecisionDiagramStudio.Infrastructure.Logging;
using DecisionDiagramStudio.Services;
using DecisionDiagramStudio.Services.Interfaces;
using DecisionDiagramStudio.ViewModels;
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
    private ILogger<App>? _logger;

    /// <summary>App クラスを初期化する。</summary>
    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => DisposeServices();
    }

    /// <summary>アプリ起動時に DI コンテナを構成し MainWindow を表示する。</summary>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();
        _logger = Services.GetRequiredService<ILogger<App>>();
        _logger.LogInformation("Application launch started.");

        _window = new MainWindow();
        _window.Activate();
        _logger.LogInformation("Application main window activated.");
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(new DecisionDiagramOptions());
        services.AddSingleton<IDiagramService, DiagramService>();
        services.AddSingleton<IGraphvizService, GraphvizService>();
        services.AddSingleton<IPresetService, PresetService>();
        services.AddSingleton<ISvgWebViewDocumentSource, SvgWebViewDocumentSource>();
        services.AddSingleton<Commands.CommandStack>();
        services.AddSingleton<WorkbenchViewModel>();
        services.AddSingleton<DiagramPanelViewModel>();
        services.AddSingleton<StatisticsViewModel>();
        services.AddSingleton<ExplanationViewModel>();

        services.AddLogging(LoggingConfiguration.Configure);
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs args)
    {
        _logger?.LogCritical(
            "Unhandled exception reached the application boundary. ExceptionType={ExceptionType}",
            args.Exception?.GetType().Name ?? "Unknown");
    }

    private static void DisposeServices()
    {
        if (Services is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
