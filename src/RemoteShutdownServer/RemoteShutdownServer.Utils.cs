using System.Diagnostics;
using System.Net.NetworkInformation;

namespace RemoteShutdownServer
{
    public partial class RemoteShutdownServer
    {
        private string GetLocalIP()
        {
            try
            {
                using (var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork,
                       System.Net.Sockets.SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 80);
                    var endPoint = socket.LocalEndPoint as System.Net.IPEndPoint;
                    return endPoint?.Address.ToString() ?? "127.0.0.1";
                }
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        private bool IsAlreadyRunning()
        {
            try
            {
                if (File.Exists(lockFile))
                {
                    var pidText = File.ReadAllText(lockFile).Trim();
                    if (int.TryParse(pidText, out int pid))
                    {
                        try
                        {
                            var process = Process.GetProcessById(pid);
                            if (process.ProcessName.Equals(Process.GetCurrentProcess().ProcessName,
                                StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                        catch
                        {
                            File.Delete(lockFile);
                        }
                    }
                }

                if (config != null && IsPortInUse(config.Port))
                    return true;

                return false;
            }
            catch
            {
                return false;
            }
        }

        private bool IsPortInUse(int port)
        {
            try
            {
                var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
                var tcpConnInfoArray = ipGlobalProperties.GetActiveTcpListeners();
                return tcpConnInfoArray.Any(endpoint => endpoint.Port == port);
            }
            catch
            {
                return false;
            }
        }

        private bool IsPortAccessible(int port)
        {
            try
            {
                var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
                var tcpListeners = ipGlobalProperties.GetActiveTcpListeners();
                return tcpListeners.Any(endpoint => endpoint.Port == port && endpoint.Address.ToString() == "0.0.0.0");
            }
            catch
            {
                return false;
            }
        }

        private void EnsureFirewallRule(int port)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    var ruleName = $"RemoteShutdownServer_Port_{port}";
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = $"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow protocol=TCP localport={port}",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using var process = Process.Start(processInfo);
                    if (process != null)
                    {
                        process.WaitForExit();
                        if (process.ExitCode == 0)
                        {
                            Console.WriteLine($"✅ Firewall rule created for port {port}");
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ Firewall rule creation failed for port {port}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Firewall configuration failed: {ex.Message}");
            }
        }

        private void CreateLockFile()
        {
            try
            {
                File.WriteAllText(lockFile, Process.GetCurrentProcess().Id.ToString());
            }
            catch { }
        }

        private string GetUptime()
        {
            if (!serverRunning || startTime == default)
                return "0:00:00";

            var uptime = DateTime.Now - startTime;
            return $"{(int)uptime.TotalHours}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
        }

        private void ShowNotification(string title, string message)
        {
            try
            {
                if (OperatingSystem.IsWindows() && trayIcon != null && trayIcon.Visible)
                {
                    trayIcon.ShowBalloonTip(10000, title, message, ToolTipIcon.None);
                }
            }
            catch { }
        }
    }
}
