using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using HockeySim.Desktop.Main;
using HockeySim.Infrastructure.Saves;
using HockeySim.Management.GameManagement;

namespace HockeySim.Desktop;

public sealed class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var gameManager = new GameManager();
            var saves = new GameSaveDirectory(GameSaveDirectory.DefaultPath());
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(gameManager, saves, () => desktop.Shutdown()),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}