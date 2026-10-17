using Microsoft.UI.Xaml;

namespace SystemCompanion;

public partial class App : Application
{
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Optional first argument picks the starting page, handy while developing:
        //   SystemCompanion.exe windows
        //   SystemCompanion.exe improve
        //   SystemCompanion.exe history
        var argv = Environment.GetCommandLineArgs();
        var startPage = argv.Length > 1 ? argv[1] : "bios";

        _window = new MainWindow(startPage);
        _window.Activate();
    }
}
