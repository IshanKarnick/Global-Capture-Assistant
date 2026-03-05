using System.Windows;
using GlobalCaptureAssistant.Runtime;

namespace GlobalCaptureAssistant;

public partial class App : System.Windows.Application
{
    private AppHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        base.OnStartup(e);

        _host = new AppHost();
        _host.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
