using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SystemCompanion.ViewModels;

namespace SystemCompanion.Views.Controls;

/// <summary>
/// The one rescan action, shown on every screen. It is unavailable while a scan runs
/// and reports a failure in the same plain sentence the shell holds.
/// </summary>
public sealed partial class RescanBar : UserControl
{
    private ShellViewModel? _shell;
    private Action<bool>? _finished;

    public RescanBar()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Binds the bar to the shell's scan state. <paramref name="finished"/> runs after
    /// every scan with true only when the reading was replaced.
    /// </summary>
    public void BindTo(ShellViewModel shell, Action<bool>? finished)
    {
        if (_shell is not null)
        {
            _shell.PropertyChanged -= Shell_PropertyChanged;
        }

        _shell = shell;
        _finished = finished;
        _shell.PropertyChanged += Shell_PropertyChanged;
        Update();
    }

    private void Shell_PropertyChanged(object? sender, PropertyChangedEventArgs e) => Update();

    private void Update()
    {
        if (_shell is null)
        {
            return;
        }

        ScanButton.IsEnabled = _shell.CanScan;
        ScanButton.Content = _shell.ScanButtonText;
        Progress.IsActive = _shell.IsScanning;
        Progress.Visibility = _shell.IsScanning ? Visibility.Visible : Visibility.Collapsed;
        Message.Text = _shell.ScanMessage;
        Message.Visibility = string.IsNullOrWhiteSpace(_shell.ScanMessage) || _shell.IsScanning
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        if (_shell is null)
        {
            return;
        }

        var succeeded = await _shell.RescanAsync();
        _finished?.Invoke(succeeded);
    }
}
