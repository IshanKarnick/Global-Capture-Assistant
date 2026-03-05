using System.Drawing;
using System.Windows.Forms;

namespace GlobalCaptureAssistant.Platform;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon? _customIcon;
    private readonly ToolStripMenuItem _captureMenuItem;
    private readonly ToolStripMenuItem _showSidebarMenuItem;
    private readonly ToolStripMenuItem _exitMenuItem;

    public TrayIconService()
    {
        _captureMenuItem = new ToolStripMenuItem("Capture Now");
        _showSidebarMenuItem = new ToolStripMenuItem("Show Sidebar");
        _exitMenuItem = new ToolStripMenuItem("Exit");

        var menu = new ContextMenuStrip();
        menu.Items.Add(_captureMenuItem);
        menu.Items.Add(_showSidebarMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_exitMenuItem);

        _customIcon = AppIconLoader.LoadTrayIcon();
        _notifyIcon = new NotifyIcon
        {
            Icon = _customIcon ?? SystemIcons.Application,
            Text = "Global Capture Assistant",
            Visible = true,
            ContextMenuStrip = menu
        };
    }

    public event EventHandler? CaptureRequested
    {
        add => _captureMenuItem.Click += value;
        remove => _captureMenuItem.Click -= value;
    }

    public event EventHandler? ShowSidebarRequested
    {
        add => _showSidebarMenuItem.Click += value;
        remove => _showSidebarMenuItem.Click -= value;
    }

    public event EventHandler? ExitRequested
    {
        add => _exitMenuItem.Click += value;
        remove => _exitMenuItem.Click -= value;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _customIcon?.Dispose();
    }
}
