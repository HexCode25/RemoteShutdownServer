using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RemoteShutdownServer
{
    public partial class RemoteShutdownServer
    {
        private void SetupSystemTray()
        {
            trayMenu = new ContextMenuStrip();

            trayMenu.Items.Add("🖥️ Dashboard", null, (s, e) => OpenDashboard());
            trayMenu.Items.Add("🌐 Server Status", null, (s, e) => ShowStatus());
            trayMenu.Items.Add("💥 Shutdown PC", null, (s, e) => ShutdownPC());
            trayMenu.Items.Add("🖥️ Close Monitor", null, (s, e) => CloseMonitor());
            trayMenu.Items.Add("🔄 Restart Server", null, (s, e) => RestartServer());
            trayMenu.Items.Add("❌ Exit", null, (s, e) => QuitApp());

            Icon? customIcon = null;
            try
            {
                var possibleIconPaths = new[]
                {
                    "shutdown.ico",
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shutdown.ico"),
                    System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "shutdown.ico")
                };

                foreach (var iconPath in possibleIconPaths)
                {
                    if (System.IO.File.Exists(iconPath))
                    {
                        try
                        {
                            customIcon = new Icon(iconPath);
                            break;
                        }
                        catch { }
                    }
                }
            }
            catch { }

            trayIcon = new NotifyIcon
            {
                Icon = customIcon ?? SystemIcons.Application,
                Text = "Remote Shutdown Server",
                ContextMenuStrip = trayMenu,
                Visible = true
            };

            trayIcon.DoubleClick += (s, e) => OpenDashboard();
        }

        private void OpenDashboard()
        {
            try
            {
                if (!serverRunning)
                {
                    StartServer();
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = $"http://localhost:{config?.Port ?? 5000}",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void ShowStatus()
        {
            var statusMessage = serverRunning ? "🟢 The server is running" : "🔴 The server is down";
            var uptime = GetUptime();
            var port = config?.Port ?? 5000;
            var host = GetLocalIP();

            var fullMessage = $"{statusMessage}\nUptime: {uptime}\nPort: {port}\nHost: {host}";
            ShowNotification("Remote Shutdown Server - Status", fullMessage);
        }

        private void ShutdownPC()
        {
            ShowNotification("Remote Shutdown Server", "The computer turns off.... 💥");

            if (OperatingSystem.IsWindows())
            {
                Process.Start("shutdown", "/s /f /t 0");
            }
            else
            {
                Process.Start("shutdown", "now");
            }
        }

        private void CloseMonitor()
        {
            ShowNotification("Remote Shutdown Server", "The monitor closes... 🖥️");

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    SendMessage(-1, 0x0112, 0xF170, 2);
                    ShowNotification("Remote Shutdown Server", "Monitor closed successfully! ✅");
                }
                catch (Exception)
                {
                    try
                    {
                        Process.Start("powershell", "-Command \"(Add-Type -Name Monitor -Namespace Win32 -PassThru -MemberDefinition '[DllImport(\\\"user32.dll\\\")] public static extern int SendMessage(int hWnd, int hMsg, int wParam, int lParam);').SendMessage(-1, 0x0112, 0xF170, 2)\"");
                        ShowNotification("Remote Shutdown Server", "Monitor closed successfully! ✅");
                    }
                    catch (Exception ex2)
                    {
                        ShowNotification("Remote Shutdown Server", $"Error: {ex2.Message}");
                    }
                }
            }
            else
            {
                ShowNotification("Remote Shutdown Server", "The Close Monitor feature is only available on Windows.! ❌");
            }
        }

        [DllImport("user32.dll")]
        private static extern int SendMessage(int hWnd, int hMsg, int wParam, int lParam);

        private void RestartServer()
        {
            ShowNotification("Remote Shutdown Server", "Server restarting... 🔄");

            Task.Run(async () =>
            {
                serverRunning = false;
                try
                {
                    if (webApp != null)
                    {
                        await webApp.StopAsync();
                    }
                }
                catch { }

                LoadConfig();
                SetupWebServer();
                StartServer();

                ShowNotification("Remote Shutdown Server", "The server has restarted successfully. ✅");

                try
                {
                    Process.Start(new ProcessStartInfo { FileName = $"http://localhost:{config?.Port ?? 5000}", UseShellExecute = true });
                }
                catch { }
            });
        }

        private void QuitApp()
        {
            ShowNotification("Remote Shutdown Server", "The server has stopped. ❌");
            SaveConfig();

            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }

            if (webApp != null)
            {
                try { Task.Run(async () => { try { await webApp.StopAsync(); } catch { } }); } catch { }
            }

            try
            {
                if (System.IO.File.Exists(lockFile))
                    System.IO.File.Delete(lockFile);
            }
            catch { }

            Environment.Exit(0);
        }
    }
}
