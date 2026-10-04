using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HockeySim.Desktop.Main;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Closing the window with unsaved progress asks first, as exiting from the menu does; the
    /// window closes once the user agrees.
    /// </summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (DataContext is MainWindowViewModel { CanCloseWithoutConfirmation: false } viewModel)
        {
            e.Cancel = true;
            viewModel.RequestExit();
        }

        base.OnClosing(e);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}