using System.ComponentModel;

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

using HockeySim.Desktop.Theme;

namespace HockeySim.Desktop.Main;

public sealed partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Follows the view model's team colours. They are set on the window so that everything in it,
    /// including the confirmation overlay and pop-ups, takes the team's identity.
    /// </summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        TeamPalette.Apply(Resources, _viewModel?.TeamColours);
        base.OnDataContextChanged(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.TeamColours))
        {
            TeamPalette.Apply(Resources, _viewModel?.TeamColours);
        }
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