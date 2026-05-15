using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DecisionDiagramStudio;

/// <summary>アプリのメインウィンドウ。NavigationView のホストとして機能する。</summary>
public sealed partial class MainWindow : Window
{
    private readonly ILogger<MainWindow> _logger;

    /// <summary>MainWindow を初期化する。</summary>
    public MainWindow()
    {
        _logger = App.Services.GetRequiredService<ILogger<MainWindow>>();
        InitializeComponent();
        RootNavigationView.SelectedItem = RootNavigationView.MenuItems[0];
        NavigateToWorkbench();
        _logger.LogInformation("Main window initialized.");
    }

    private void RootNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem { Tag: "Workbench" })
        {
            _logger.LogInformation("Navigation requested. Target=Workbench");
            NavigateToWorkbench();
        }
    }

    private void NavigateToWorkbench()
    {
        if (ContentFrame.Content?.GetType() != typeof(Views.WorkbenchPage))
        {
            ContentFrame.Navigate(typeof(Views.WorkbenchPage));
            _logger.LogInformation("Navigation completed. Target=Workbench");
        }
    }
}
