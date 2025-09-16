using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Win32;
using System.Drawing; // Added for Icon

namespace RemoteShutdownServer
{
    public class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            var server = new RemoteShutdownServer();
            server.Run();
        }
    }

    public class RemoteShutdownServer : Form
    {
        private NotifyIcon? trayIcon;
        private ContextMenuStrip? trayMenu;
        private WebApplication? webApp;
        private bool serverRunning = false;
        private DateTime startTime;
        private ServerConfig? config;
        private readonly string configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shutdown_server_config.json");
        private readonly string lockFile = "remote_shutdown.lock";

        public RemoteShutdownServer()
        {
            InitializeComponent();
            LoadConfig();
            
            if (IsAlreadyRunning())
            {
                MessageBox.Show("An instance is already running.", "Remote Shutdown Server", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Environment.Exit(1);
            }

            CreateLockFile();
            SetupSystemTray(); 
            SetupWebServer();
        }

        private void InitializeComponent()
        {
            this.Text = "Remote Shutdown Server";
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Opacity = 0;
            this.Visible = false;
            this.ShowIcon = false;
            this.Size = new System.Drawing.Size(0, 0);
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new System.Drawing.Point(-10000, -10000);
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(configFile))
                {
                    var fileContent = File.ReadAllText(configFile);
                    config = DecryptConfig(fileContent);

                    // Check if the configuration loaded correctly
                    if (config == null)
                    {
                        Console.WriteLine("Config loaded as null, using defaults");
                        config = CreateDefaultConfig();
                        SaveConfig();
                    }
                    else
                    {
                        Console.WriteLine($"Config loaded successfully: Port={config.Port}, Host={config.Host}, SecretKey={config.SecretKey}");
                    }
                    
                    if (OperatingSystem.IsWindows())
                    {
                        var realStartupStatus = CheckStartupStatus();
                        if (config.RunOnStartup != realStartupStatus)
                        {
                            Console.WriteLine($"Syncing startup status: {config.RunOnStartup} -> {realStartupStatus}");
                            config.RunOnStartup = realStartupStatus;
                            SaveConfig();
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Config file not found, creating default");
                    config = CreateDefaultConfig();
                    SaveConfig();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Config load error: {ex.Message}");
                config = CreateDefaultConfig();
                SaveConfig(); 
            }
        }

        private ServerConfig CreateDefaultConfig()
        {
            return new ServerConfig
            {
                SecretKey = "1234",
                Port = 5000,
                Host = GetLocalIP(),
                RunOnStartup = false,
                AutoOpenBrowser = false
            };
        }

        private void SaveConfig()
        {
            try
            {
                if (config != null)
                {
                    var encryptedData = EncryptConfig(config);
                    File.WriteAllText(configFile, encryptedData);
                    Console.WriteLine($"Configuration saved successfully to: {configFile}");
                    Console.WriteLine($"Config content: Port={config.Port}, Host={config.Host}, SecretKey={config.SecretKey}, RunOnStartup={config.RunOnStartup}");
                }
                else
                {
                    Console.WriteLine("Cannot save config: config is null");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Config save error: {ex.Message}");
                Console.WriteLine($"Attempted to save to: {configFile}");
            }
        }

        private string EncryptConfig(ServerConfig config)
        {
            try
            {
                var systemInfo = $"{Environment.OSVersion.Platform}-{Environment.MachineName}-{Environment.UserName}";
                var key = SHA256.HashData(Encoding.UTF8.GetBytes(systemInfo));
                
                var json = JsonSerializer.Serialize(config);
                var encrypted = new byte[json.Length];
                
                for (int i = 0; i < json.Length; i++)
                {
                    encrypted[i] = (byte)(json[i] ^ key[i % key.Length]);
                }
                
                return Convert.ToBase64String(encrypted);
            }
            catch
            {
                return JsonSerializer.Serialize(config);
            }
        }

        private ServerConfig? DecryptConfig(string fileContent)
        {
            try
            {
                var result = JsonSerializer.Deserialize<ServerConfig>(fileContent);
                if (result != null)
                {
                    Console.WriteLine("Configuration loaded as plain JSON");
                    return result;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Plain JSON load failed: {ex.Message}");
            }
            
            try
            {
                var systemInfo = $"{Environment.OSVersion.Platform}-{Environment.MachineName}-{Environment.UserName}";
                var key = SHA256.HashData(Encoding.UTF8.GetBytes(systemInfo));
                
                var encryptedBytes = Convert.FromBase64String(fileContent);
                var decrypted = new char[encryptedBytes.Length];
                
                for (int i = 0; i < encryptedBytes.Length; i++)
                {
                    decrypted[i] = (char)(encryptedBytes[i] ^ key[i % key.Length]);
                }
                
                var result = JsonSerializer.Deserialize<ServerConfig>(new string(decrypted));
                if (result != null)
                {
                    Console.WriteLine("Configuration decrypted successfully");
                    return result;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Decrypt failed: {ex.Message}");
            }
            
            Console.WriteLine("All config loading methods failed, returning null");
            return null;
        }

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
                    var command = $"netsh advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow protocol=TCP localport={port}";
                    
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

        private void SetupSystemTray()
        {
            trayMenu = new ContextMenuStrip();
            
            trayMenu.Items.Add("🖥️ Dashboard", null, (s, e) => OpenDashboard());
            trayMenu.Items.Add("🌐 Server Status", null, (s, e) => ShowStatus());
            trayMenu.Items.Add("💥 Shutdown PC", null, (s, e) => ShutdownPC());
            trayMenu.Items.Add("🖥️ Close Monitor", null, (s, e) => CloseMonitor());
            trayMenu.Items.Add("-"); 
            trayMenu.Items.Add("ℹ️ About", null, (s, e) => ShowAbout());
            trayMenu.Items.Add("💝 Donate", null, (s, e) => OpenDonate());
            trayMenu.Items.Add("🔄 Restart Server", null, (s, e) => RestartServer());
            trayMenu.Items.Add("❌ Exit", null, (s, e) => QuitApp());

            Icon? customIcon = null;
            try
            {
                var possibleIconPaths = new[]
                {
                    "shutdown.ico",  
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shutdown.ico"), 
                    Path.Combine(Directory.GetCurrentDirectory(), "shutdown.ico") 
                };

                foreach (var iconPath in possibleIconPaths)
                {
                    if (File.Exists(iconPath))
                    {
                        try
                        {
                            customIcon = new Icon(iconPath);
                            break; 
                        }
                        catch
                        {
                            continue;
                        }
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

        private void SetupWebServer()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                serverOptions.Listen(System.Net.IPAddress.Any, config?.Port ?? 5000);
            });

            var app = builder.Build();
            app.UseStaticFiles();
            app.UseCors();

            app.MapGet("/", (HttpContext context) =>
            {
                context.Response.Redirect("/login");
                return Task.CompletedTask;
            });

            app.MapGet("/login", async (HttpContext context) =>
            {
                var html = GenerateLoginHtml();
                await context.Response.WriteAsync(html);
            });

            app.MapPost("/login", async (HttpContext context) =>
            {
                var form = await context.Request.ReadFormAsync();
                var password = form["password"].ToString();

                if (config != null && password == config.SecretKey)
                {
                    var html = GenerateDashboardHtml();
                    await context.Response.WriteAsync(html);
                }
                else
                {
                    var html = GenerateLoginHtml("Incorrect password!");
                    await context.Response.WriteAsync(html);
                }
            });


            app.MapGet("/favicon.ico", async (HttpContext context) =>
            {
                var iconPath = "shutdown.ico";
                if (File.Exists(iconPath))
                {
                    var iconBytes = await File.ReadAllBytesAsync(iconPath);
                    context.Response.ContentType = "image/x-icon";
                    await context.Response.Body.WriteAsync(iconBytes);
                }
                else
                {
                    context.Response.StatusCode = 404;
                }
            });

            // Simple endpoint for shutdown without authentication (like in Python)
            app.MapGet("/shutdown", async (HttpContext context) =>
            {
                var key = context.Request.Query["key"].ToString();
                
                if (key == (config?.SecretKey ?? "1234"))
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
                    
                    context.Response.ContentType = "text/html; charset=utf-8";
                    await context.Response.WriteAsync("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Shutdown Command</title></head><body style='font-family: Arial, sans-serif; text-align: center; padding: 50px;'><h1 style='color: #4CAF50;'>✅ Shutdown Command Sent!</h1><p>Computer will shutdown in 0 seconds.</p><p style='color: #666; font-size: 14px;'>You can close this page.</p></body></html>");
                }
                else
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync("Unauthorized - Invalid key");
                }
            });

            // Endpoint for restart without authentication
            app.MapGet("/restart", async (HttpContext context) =>
            {
                var key = context.Request.Query["key"].ToString();
                
                if (key == (config?.SecretKey ?? "1234"))
                {
                    ShowNotification("Remote Shutdown Server", "Restarting the computer... 🔄");
                    
                    if (OperatingSystem.IsWindows())
                    {
                        Process.Start("shutdown", "/r /f /t 0");
                    }
                    else
                    {
                        Process.Start("reboot");
                    }
                    
                    context.Response.ContentType = "text/html; charset=utf-8";
                    await context.Response.WriteAsync("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Restart Command</title></head><body style='font-family: Arial, sans-serif; text-align: center; padding: 50px;'><h1 style='color: #2196F3;'>🔄 Restart Command Sent!</h1><p>Computer will restart in 0 seconds.</p><p style='color: #666; font-size: 14px;'>You can close this page.</p></body></html>");
                }
                else
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync("Unauthorized - Invalid key");
                }
            });

            // Endpoint for logout without authentication
            app.MapGet("/logout_user", async (HttpContext context) =>
            {
                var key = context.Request.Query["key"].ToString();
                
                if (key == (config?.SecretKey ?? "1234"))
                {
                    ShowNotification("Remote Shutdown Server", "User is logging out... 🔒");
                    
                    if (OperatingSystem.IsWindows())
                    {
                        Process.Start("shutdown", "/l /f");
                    }
                    else
                    {
                        Process.Start("pkill", "-u $USER");
                    }
                    
                    context.Response.ContentType = "text/html; charset=utf-8";
                    await context.Response.WriteAsync("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Logout Command</title></head><body style='font-family: Arial, sans-serif; text-align: center; padding: 50px;'><h1 style='color: #FF9800;'>🔒 Logout Command Sent!</h1><p>User will be logged out.</p><p style='color: #666; font-size: 14px;'>You can close this page.</p></body></html>");
                }
                else
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync("Unauthorized - Invalid key");
                }
            });

            // Endpoint for sleep without authentication
            app.MapGet("/sleep", async (HttpContext context) =>
            {
                var key = context.Request.Query["key"].ToString();
                
                if (key == (config?.SecretKey ?? "1234"))
                {
                    ShowNotification("Remote Shutdown Server", "The computer goes into sleep mode.... 😴");
                    
                    if (OperatingSystem.IsWindows())
                    {
                        Process.Start("powercfg", "/hibernate off");
                        Process.Start("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0");
                    }
                    else
                    {
                        Process.Start("systemctl", "suspend");
                    }
                    
                    context.Response.ContentType = "text/html; charset=utf-8";
                    await context.Response.WriteAsync("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Sleep Command</title></head><body style='font-family: Arial, sans-serif; text-align: center; padding: 50px;'><h1 style='color: #9C27B0;'>😴 Sleep Command Sent!</h1><p>Computer will go to sleep.</p><p style='color: #666; font-size: 14px;'>You can close this page.</p></body></html>");
                }
                else
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync("Unauthorized - Invalid key");
                }
            });

            // Endpoint for hibernate without authentication
            app.MapGet("/hibernate", async (HttpContext context) =>
            {
                var key = context.Request.Query["key"].ToString();
                
                if (key == (config?.SecretKey ?? "1234"))
                {
                    ShowNotification("Remote Shutdown Server", "The computer goes into hibernation.... 🐻");
                    
                    if (OperatingSystem.IsWindows())
                    {
                        Process.Start("shutdown", "/h /f");
                    }
                    else
                    {
                        Process.Start("systemctl", "hibernate");
                    }
                    
                    context.Response.ContentType = "text/html; charset=utf-8";
                    await context.Response.WriteAsync("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Hibernate Command</title></head><body style='font-family: Arial, sans-serif; text-align: center; padding: 50px;'><h1 style='color: #795548;'>🐻 Hibernate Command Sent!</h1><p>Computer will hibernate.</p><p style='color: #666; font-size: 14px;'>You can close this page.</p></body></html>");
                }
                else
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync("Unauthorized - Invalid key");
                }
            });

            app.MapPost("/api/shutdown", async (HttpContext context) =>
            {
                // We verify the password in the request body
                try
                {
                    var requestBody = await context.Request.ReadFromJsonAsync<Dictionary<string, object>>();
                    var key = requestBody?["key"]?.ToString();
                    
                    if (key != (config?.SecretKey ?? "1234"))
                    {
                        context.Response.StatusCode = 401;
                        await context.Response.WriteAsync("Unauthorized - Invalid key");
                        return;
                    }
                }
                catch
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("Bad Request - Missing key");
                    return;
                }

                ShowNotification("Remote Shutdown Server", "The computer turns off.... 💥");
                
                if (OperatingSystem.IsWindows())
                {
                    Process.Start("shutdown", "/s /f /t 0");
                }
                else
                {
                    Process.Start("shutdown", "now");
                }

                await context.Response.WriteAsync("Shutdown command sent!");
            });

            app.MapGet("/api/status", async (HttpContext context) =>
            {
                // We verify the password in the query string
                var key = context.Request.Query["key"].ToString();
                if (key != (config?.SecretKey ?? "1234"))
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync("Unauthorized - Invalid key");
                    return;
                }

                var status = new
                {
                    status = serverRunning ? "running" : "stopped",
                    uptime = GetUptime(),
                    port = config?.Port ?? 5000,
                    host = config?.Host ?? "127.0.0.1",
                    startTime = startTime.ToString("O"),
                    runOnStartup = config?.RunOnStartup ?? false
                };

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(status));
            });

            // API endpoint for server testing
            app.MapGet("/api/test", async (HttpContext context) =>
            {
                // We no longer check the session - anyone can test the server
                var result = new { message = "Server is running perfectly! ✅" };
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(result));
            });



            // API endpoint for configuration
            app.MapPost("/api/config", async (HttpContext context) =>
            {
                // We verify the password in the request body
                try
                {
                    var configData = await context.Request.ReadFromJsonAsync<Dictionary<string, object>>();
                    
                    if (configData == null || !configData.ContainsKey("key"))
                    {
                        context.Response.StatusCode = 400;
                        var result = new { message = "Missing authentication key" };
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                        return;
                    }

                    var key = configData["key"].ToString();
                    if (key != (config?.SecretKey ?? "1234"))
                    {
                        context.Response.StatusCode = 401;
                        var result = new { message = "Unauthorized - Invalid key" };
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                        return;
                    }
                    
                    if (config != null)
                    {
                        bool needsRestart = false;
                        var oldPort = config.Port;
                        var oldHost = config.Host;
                        
                        if (configData.ContainsKey("port") && int.TryParse(configData["port"].ToString(), out int port))
                        {
                            if (config.Port != port)
                            {
                                config.Port = port;
                                needsRestart = true;
                            }
                        }
                        if (configData.ContainsKey("secret_key") && configData["secret_key"] != null)
                            config.SecretKey = configData["secret_key"].ToString() ?? config.SecretKey;
                        
                        SaveConfig();
                        
                        if (needsRestart)
                        {
                            var result = new { message = "Configuration saved! Server will restart automatically to apply changes. 🔄" };
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                            
                            // Restart server în background
                            _ = Task.Run(async () =>
                            {
                                await Task.Delay(1000); // Wait 1 second for response
                                RestartServer();
                            });
                        }
                        else
                        {
                            var result = new { message = "Configuration saved successfully! 💾" };
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                        }
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                        var result = new { message = "Invalid configuration data" };
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                    }
                }
                catch
                {
                    context.Response.StatusCode = 500;
                    var result = new { message = "Failed to save configuration" };
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                }
            });

            // API endpoint pentru management startup
            app.MapPost("/api/startup", async (HttpContext context) =>
            {
                // We verify the password in the request body
                try
                {
                    var startupData = await context.Request.ReadFromJsonAsync<Dictionary<string, object>>();
                    
                    if (startupData == null || !startupData.ContainsKey("key"))
                    {
                        context.Response.StatusCode = 400;
                        var result = new { message = "Missing authentication key" };
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                        return;
                    }

                    var key = startupData["key"].ToString();
                    if (key != (config?.SecretKey ?? "1234"))
                    {
                        context.Response.StatusCode = 401;
                        var result = new { message = "Unauthorized - Invalid key" };
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                        return;
                    }
                    
                    if (startupData.ContainsKey("action"))
                    {
                        var action = startupData["action"].ToString();
                        bool success = false;
                        string message = "";

                        if (OperatingSystem.IsWindows())
                        {
                            using var registryKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                            
                            if (action == "add")
                            {
                                var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                                if (!string.IsNullOrEmpty(exePath))
                                {
                                    registryKey?.SetValue("RemoteShutdownServer", exePath);
                                    if (config != null)
                                        config.RunOnStartup = true;
                                    success = true;
                                    message = "Added to Windows startup successfully! ✅";
                                }
                            }
                            else if (action == "remove")
                            {
                                registryKey?.DeleteValue("RemoteShutdownServer", false);
                                if (config != null)
                                    config.RunOnStartup = false;
                                success = true;
                                message = "Removed from Windows startup successfully! ❌";
                            }
                        }

                        if (success)
                        {
                            SaveConfig();
                            var result = new { message = message };
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                        }
                        else
                        {
                            context.Response.StatusCode = 400;
                            var result = new { message = "Failed to update startup settings" };
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                        }
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                        var result = new { message = "Invalid startup action" };
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                    }
                }
                catch
                {
                    context.Response.StatusCode = 500;
                    var result = new { message = "Failed to update startup settings" };
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                }
            });

            webApp = app;
        }

        private string GenerateDashboardHtml()
        {
            return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Remote Shutdown Server - Dashboard</title>
    <link rel=""icon"" type=""image/x-icon"" href=""/favicon.ico"">
    <style>
        /* ===== MAIN STYLES - Remote Shutdown Server ===== */
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            color: #333;
        }}
        
        .container {{
            max-width: 1200px;
            margin: 0 auto;
            padding: 20px;
        }}
        
        .header {{
            text-align: center;
            margin-bottom: 30px;
            color: white;
        }}
        
        .header h1 {{
            font-size: 2.5rem;
            margin-bottom: 10px;
            text-shadow: 2px 2px 4px rgba(0,0,0,0.3);
        }}
        
        .header p {{
            font-size: 1.1rem;
            opacity: 0.9;
        }}
        
        .user-controls {{
            margin-top: 15px;
        }}
        
        .user-controls .btn {{
            padding: 8px 16px;
            font-size: 0.9rem;
        }}
        
        .card {{
            background: white;
            border-radius: 15px;
            padding: 25px;
            box-shadow: 0 10px 30px rgba(0,0,0,0.1);
            transition: transform 0.3s ease, box-shadow 0.3s ease;
        }}
        
        .card:hover {{
            transform: translateY(-5px);
            box-shadow: 0 15px 40px rgba(0,0,0,0.15);
        }}
        
        .card h3 {{
            color: #667eea;
            margin-bottom: 15px;
            font-size: 1.3rem;
            border-bottom: 2px solid #f0f0f0;
            padding-bottom: 10px;
        }}
        
        .status-indicator {{
            display: inline-block;
            width: 12px;
            height: 12px;
            border-radius: 50%;
            margin-right: 8px;
        }}
        
        .status-running {{
            background-color: #4CAF50;
            box-shadow: 0 0 10px #4CAF50;
        }}
        
        .status-stopped {{
            background-color: #f44336;
            box-shadow: 0 0 10px #f44336;
        }}
        
        .btn {{
            background: linear-gradient(45deg, #667eea, #764ba2);
            color: white;
            border: none;
            padding: 12px 24px;
            border-radius: 25px;
            cursor: pointer;
            font-size: 1rem;
            font-weight: 500;
            transition: all 0.3s ease;
            margin: 5px;
            text-decoration: none;
            display: inline-block;
        }}
        
        .btn:hover {{
            transform: translateY(-2px);
            box-shadow: 0 5px 15px rgba(0,0,0,0.2);
        }}
        
        .btn-success {{
            background: linear-gradient(45deg, #4CAF50, #45a049);
        }}
        
        .btn-danger {{
            background: linear-gradient(45deg, #f44336, #da190b);
        }}
        
        .form-group {{
            margin-bottom: 20px;
        }}
        
        .form-group label {{
            display: block;
            margin-bottom: 5px;
            font-weight: 500;
            color: #555;
        }}
        
        .form-group input {{
            width: 100%;
            padding: 10px;
            border: 2px solid #e0e0e0;
            border-radius: 8px;
            font-size: 1rem;
            transition: border-color 0.3s ease;
        }}
        
        .form-group input:focus {{
            outline: none;
            border-color: #667eea;
        }}
        
        .alert {{
            padding: 15px;
            border-radius: 8px;
            margin-bottom: 20px;
            display: none;
        }}
        
        .alert-success {{
            background-color: #d4edda;
            color: #155724;
            border: 1px solid #c3e6cb;
        }}
        
        .alert-error {{
            background-color: #f8d7da;
            color: #721c24;
            border: 1px solid #f5c6cb;
        }}
        
        .uptime {{
            font-size: 1.5rem;
            font-weight: bold;
            color: #667eea;
            text-align: center;
            margin: 20px 0;
        }}
        
        .quick-actions {{
            text-align: center;
            margin-top: 20px;
        }}
        
        .dashboard-url, .shutdown-url {{
            background: #f8f9fa;
            border: 2px solid #e9ecef;
            border-radius: 8px;
            padding: 15px;
            margin: 15px 0;
            font-family: 'Courier New', monospace;
            word-break: break-all;
            position: relative;
            padding-right: 80px; /* Space for copy button */
        }}
        
        .copy-btn {{
            background: #6c757d;
            color: white;
            border: none;
            padding: 8px 16px;
            border-radius: 5px;
            cursor: pointer;
            font-size: 0.9rem;
            position: absolute;
            right: 15px;
            top: 50%;
            transform: translateY(-50%);
        }}
        
        .copy-btn:hover {{
            background: #5a6268;
        }}
        
        /* ===== DASHBOARD SPECIFIC STYLES ===== */
        .dashboard {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
            gap: 20px;
            margin-bottom: 30px;
        }}
        
        /* Dashboard specific card styles */
        #server-status-card .uptime {{
            display: flex;
            align-items: center;
            justify-content: center;
            gap: 10px;
        }}
        
        #server-status-card .status-indicator {{
            width: 16px;
            height: 16px;
        }}
        
        #startup-card .status-indicator {{
            width: 14px;
            height: 14px;
        }}
        
        /* Status indicators with better visibility */
        .status-indicator.status-running {{
            animation: pulse 2s infinite;
        }}
        
        .status-indicator.status-stopped {{
            animation: blink 1.5s infinite;
        }}
        
        @keyframes pulse {{
            0% {{ opacity: 1; }}
            50% {{ opacity: 0.7; }}
            100% {{ opacity: 1; }}
        }}
        
        @keyframes blink {{
            0%, 50% {{ opacity: 1; }}
            51%, 100% {{ opacity: 0.5; }}
        }}
        
        /* Responsive Design */
        @media (max-width: 768px) {{
            .container {{
                padding: 10px;
            }}
            
            .header h1 {{
                font-size: 2rem;
            }}
            
            .dashboard {{
                grid-template-columns: 1fr;
            }}
            
            .btn {{
                display: block;
                width: 100%;
                margin: 5px 0;
            }}
        }}
    </style>
</head>
<body>
    <div class=""container"">
                 <div class=""header"">
             <h1>🖥️ Remote Shutdown Server</h1>
             <p>Control your computer remotely via web interface</p>
         </div>

        <div class=""alert alert-success"" id=""successAlert""></div>
        <div class=""alert alert-error"" id=""errorAlert""></div>

        <div class=""dashboard"">
            <!-- Status Card -->
            <div class=""card"" id=""server-status-card"">
                <h3>📊 Server Status</h3>
                <div class=""uptime"">
                    <span class=""status-indicator status-{(serverRunning ? "running" : "stopped")}"" id=""server-status-indicator""></span>
                    <span id=""server-status-text"">{(serverRunning ? "Running" : "Stopped")}</span>
                </div>
                <p><strong>Uptime:</strong> <span id=""uptime"">{GetUptime()}</span></p>
                                 <p><strong>Port:</strong> <span id=""port-display"">{config?.Port ?? 5000}</span></p>
                 <p><strong>Host:</strong> <span id=""host-display"">{GetLocalIP()}</span></p>
                <div class=""quick-actions"">
                    <button class=""btn btn-success"" onclick=""testServer()"">🧪 Test Server</button>
                    <button class=""btn"" onclick=""refreshStatus()"">🔄 Refresh</button>
                </div>
            </div>

            <!-- Shutdown Card -->
            <div class=""card"">
                <h3>💥 Remote Shutdown</h3>
                <div class=""form-group"">
                    <label for=""shutdownKey"">Password:</label>
                    <input type=""password"" id=""shutdownKey"" placeholder=""Enter password"" value=""{config?.SecretKey ?? "1234"}"">
                </div>
                <div class=""quick-actions"">
                    <button class=""btn btn-danger"" onclick=""shutdownComputer()"">🚀 Shutdown Computer</button>
                </div>
            </div>

            <!-- Configuration Card -->
            <div class=""card"">
                <h3>⚙️ Configuration</h3>
                <div class=""form-group"">
                    <label for=""port"">Port:</label>
                    <input type=""number"" id=""port"" value=""{config?.Port ?? 5000}"" min=""1"" max=""65535"">
                </div>
                <div class=""form-group"">
                    <label for=""secretKey"">Secret Key:</label>
                    <input type=""password"" id=""secretKey"" value=""{config?.SecretKey ?? "1234"}"">
                </div>
                <div class=""quick-actions"">
                    <button class=""btn"" onclick=""saveConfig()"">💾 Save Settings</button>
                </div>
            </div>

            <!-- Startup Card -->
            <div class=""card"" id=""startup-card"">
                <h3>🚀 Startup Settings</h3>
                <p><strong>Current Status:</strong> 
                    <span class=""status-indicator status-{(config?.RunOnStartup == true ? "running" : "stopped")}"" id=""startup-status-indicator""></span>
                    <span id=""startup-status-text"">{(config?.RunOnStartup == true ? "Added to startup" : "Not in startup")}</span>
                </p>
                <p style=""font-size: 0.9rem; color: #666; margin-bottom: 15px;"">
                    ✅ Startup status-ul se sincronizează automat cu Windows Registry
                </p>
                <div class=""quick-actions"">
                    {(config?.RunOnStartup == true ? 
                        "<button class=\"btn btn-danger\" onclick=\"toggleStartup('remove')\">❌ Remove from Startup</button>" :
                        "<button class=\"btn btn-success\" onclick=\"toggleStartup('add')\">✅ Add to Startup</button>")}
                </div>
            </div>

            <!-- Mobile Access Card -->
            <div class=""card"">
                <h3>📱 Remote Access</h3>
                <p>Access this dashboard from any device on your network:</p>
                
                                 <div class=""shutdown-url"">
                     <strong>Shutdown URL:</strong><br>
                     http://{GetLocalIP()}:{config?.Port ?? 5000}/shutdown?key={config?.SecretKey ?? "1234"}
                     <button class=""copy-btn"" onclick=""copyShutdownUrl()"">📋 Copy</button>
                     <p style=""margin-top: 10px; font-size: 0.9rem; color: #666;"">
                         💡 This URL includes your current password
                     </p>
                 </div>
                
                <p style=""margin-top: 20px; font-size: 0.9rem; color: #666;"">
                    💡 Perfect for controlling your computer from your phone!<br>
                    🔒 Password required for shutdown actions.
                </p>
            </div>

            <!-- iPhone Shortcuts Card -->
            <div class=""card"">
                <h3>🍎 iPhone Shortcuts Setup</h3>
                <p>Create a shortcut on your iPhone to control your computer:</p>
                
                <div style=""margin-top: 20px; font-size: 0.9rem; color: #555;"">
                    <h4 style=""color: #667eea; margin-bottom: 10px;"">📋 Setup Steps:</h4>
                    <ol style=""margin-left: 20px; line-height: 1.6;"">
                        <li>Open <strong>Shortcuts</strong> app on iPhone</li>
                        <li>Tap <strong>""+""</strong> to create new shortcut</li>
                        <li>Search and add <strong>""Get Contents of URL""</strong> action</li>
                        <li>Use this URL: <code>http://{GetLocalIP()}:{config?.Port ?? 5000}/shutdown?key={config?.SecretKey ?? "1234"}</code></li>
                        <li>Set method to <strong>""GET""</strong></li>
                        <li>Add <strong>""Show Result""</strong> action for confirmation</li>
                        <li>Save as <strong>""Shutdown PC""</strong></li>
                    </ol>
                </div>
            </div>
        </div>
    </div>

    <script>
        /* ===== DASHBOARD JAVASCRIPT FUNCTIONS ===== */
        // Global variables
        let startTime = null; // Timpul când s-a pornit serverul
        let uptimeInterval = null; // Referința la interval-ul de actualizare
        
        // ===== INITIALIZATION =====
        document.addEventListener('DOMContentLoaded', function() {{
            // Try to restore the start time from localStorage
            restoreStartTime();
            
            // Try to synchronize uptime with the server
            syncUptime();
            
            // Synchronize startup status
            syncStartupStatus();
        }});
        
        // ===== UPTIME MANAGEMENT =====
        function saveStartTime() {{
            if (startTime) {{
                localStorage.setItem('serverStartTime', startTime.toISOString());
                console.log('Start time saved to localStorage:', startTime);
            }}
        }}
        
        function restoreStartTime() {{
            try {{
                const savedStartTime = localStorage.getItem('serverStartTime');
                if (savedStartTime) {{
                    startTime = new Date(savedStartTime);
                    console.log('Start time restored from localStorage:', startTime);
                    
                    // Check if the saved time is not too old (more than 24 hours)
                    const now = new Date();
                    const diff = now - startTime;
                    const maxAge = 24 * 60 * 60 * 1000; // 24 ore în milisecunde
                    
                    if (diff > maxAge) {{
                        console.log('Saved start time is too old, clearing localStorage');
                        localStorage.removeItem('serverStartTime');
                        startTime = null;
                    }} else {{
                        Start the timer with the time restored
                        startUptimeCounter();
                    }}
                }} else {{
                    console.log('No saved start time found in localStorage');
                }}
            }} catch (error) {{
                console.log('Error restoring start time:', error);
                localStorage.removeItem('serverStartTime');
            }}
        }}
        
        function startUptimeCounter() {{
            // Stop existing interval if it exists
            if (uptimeInterval) {{
                clearInterval(uptimeInterval);
            }}
            
            // New update interval starts
            uptimeInterval = setInterval(() => {{
                updateUptimeDisplay();
            }}, 1000);
            
            console.log('Uptime counter started');
        }}
        
        function stopUptimeCounter() {{
            if (uptimeInterval) {{
                clearInterval(uptimeInterval);
                uptimeInterval = null;
                console.log('Uptime counter stopped');
            }}
        }}
        
        function updateUptimeDisplay() {{
            if (startTime) {{
                const now = new Date();
                const diff = now - startTime;
                const hours = Math.floor(diff / (1000 * 60 * 60));
                const minutes = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));
                const seconds = Math.floor((diff % (1000 * 60)) / 1000);
                
                const uptimeElement = document.getElementById('uptime');
                if (uptimeElement) {{
                    uptimeElement.textContent = `${{hours}}:${{minutes.toString().padStart(2, '0')}}:${{seconds.toString().padStart(2, '0')}}`;
                }}
            }} else {{
                // If startTime is not set, displays 0:00:00
                const uptimeElement = document.getElementById('uptime');
                if (uptimeElement) {{
                    uptimeElement.textContent = '0:00:00';
                }}
            }}
        }}
        
        async function syncUptime() {{
            try {{
                const keyElement = document.getElementById('shutdownKey');
                if (!keyElement) {{
                    console.log('shutdownKey element not found, using default');
                    // Use default password for sync
                    await syncWithServer('1234');
                    return;
                }}
                
                const key = keyElement.value || '1234';
                await syncWithServer(key);
            }} catch (error) {{
                console.log('Could not sync uptime with server:', error);
                // If synchronization fails, start the timer with the current time
                startTime = new Date();
                startUptimeCounter();
            }}
        }}
        
        async function syncWithServer(key) {{
            console.log('Syncing uptime with server...');
            try {{
                const response = await fetch(`/api/status?key=${{key}}`);
                const data = await response.json();
                
                console.log('Server response:', data);
                
                if (response.ok && data.start_time && data.status === 'running') {{
                    startTime = new Date(data.start_time);
                    console.log('✅ Uptime synced with server:', startTime);
                    console.log('Server start time:', data.start_time);
                    saveStartTime(); // Salvează timpul în localStorage
                    startUptimeCounter();
                }} else {{
                    console.log('❌ Server not running or invalid response:', data);
                    // If the server is not running, start the local timer
                    startTime = new Date();
                    console.log('Starting local timer with current time:', startTime);
                    saveStartTime(); // Salvează timpul în localStorage
                    startUptimeCounter();
                }}
            }} catch (error) {{
                console.log('❌ Error syncing with server:', error);
                // If there is an error, start the local timer
                startTime = new Date();
                console.log('Starting local timer due to error:', startTime);
                saveStartTime(); // Salvează timpul în localStorage
                startUptimeCounter();
            }}
        }}
        
        // ===== ALERT SYSTEM =====
        function showAlert(message, type) {{
            const alertElement = document.getElementById(type === 'success' ? 'successAlert' : 'errorAlert');
            alertElement.textContent = message;
            alertElement.style.display = 'block';
            
            setTimeout(() => {{
                alertElement.style.display = 'none';
            }}, 5000);
        }}
        
        // ===== SHUTDOWN FUNCTIONS =====
        async function shutdownComputer() {{
            const key = document.getElementById('shutdownKey').value;
            if (!key) {{
                showAlert('Please enter the password', 'error');
                return;
            }}
            
            if (confirm('Are you sure you want to shutdown your computer?')) {{
                try {{
                    const response = await fetch('/api/shutdown', {{
                        method: 'POST',
                        headers: {{
                            'Content-Type': 'application/json',
                        }},
                        body: JSON.stringify({{ key: key }})
                    }});
                    
                    const data = await response.json();
                    if (response.ok) {{
                        showAlert('Shutdown command sent!', 'success');
                    }} else {{
                        showAlert(data.error || 'Shutdown failed', 'error');
                    }}
                }} catch (error) {{
                    showAlert('Failed to send shutdown command: ' + error.message, 'error');
                }}
            }}
        }}
        
                 // ===== CONFIGURATION FUNCTIONS =====
         async function saveConfig() {{
             const config = {{
                 key: document.getElementById('shutdownKey').value,
                 port: parseInt(document.getElementById('port').value),
                 secret_key: document.getElementById('secretKey').value.trim()
             }};
             
             try {{
                 const response = await fetch('/api/config', {{
                     method: 'POST',
                     headers: {{
                         'Content-Type': 'application/json',
                     }},
                     body: JSON.stringify(config)
                 }});

                 const data = await response.json();
                 if (response.ok) {{
                     showAlert(data.message, 'success');
                     setTimeout(() => {{
                         location.reload();
                     }}, 2000);
                 }} else {{
                     showAlert(data.message, 'error');
                 }}
             }} catch (error) {{
                 showAlert('Failed to save configuration: ' + error.message, 'error');
             }}
         }}
        
                 // ===== STARTUP MANAGEMENT =====
         async function toggleStartup(action) {{
             try {{
                 const response = await fetch('/api/startup', {{
                     method: 'POST',
                     headers: {{
                         'Content-Type': 'application/json',
                     }},
                     body: JSON.stringify({{ 
                         key: document.getElementById('shutdownKey').value,
                         action: action 
                     }})
                 }});

                 const data = await response.json();
                 if (response.ok) {{
                     showAlert(data.message, 'success');
                     setTimeout(() => {{
                         location.reload();
                     }}, 2000);
                 }} else {{
                     showAlert(data.message, 'error');
                 }}
             }} catch (error) {{
                 showAlert('Failed to update startup settings: ' + error.message, 'error');
             }}
         }}
        
                 // ===== STATUS REFRESH =====
         async function refreshStatus() {{
             try {{
                 const key = document.getElementById('shutdownKey').value || '1234';
                 const response = await fetch(`/api/status?key=${{key}}`);
                 const data = await response.json();
                 
                 if (!response.ok) {{
                     showAlert(data.message || 'Failed to refresh status', 'error');
                     return;
                 }}
                 
                 // Actualizează uptime-ul dacă serverul rulează
                 if (data.start_time && data.status === 'running') {{
                     startTime = new Date(data.start_time);
                     saveStartTime(); // Salvează timpul în localStorage
                     startUptimeCounter();
                     console.log('✅ Uptime refreshed with server time:', startTime);
                     console.log('Server start time from refresh:', data.start_time);
                 }} else {{
                     console.log('❌ Server not running during refresh, keeping current timer');
                 }}
                 
                 // Update startup status if it has changed
                 if (data.run_on_startup !== undefined) {{
                     updateStartupStatus(data.run_on_startup);
                 }}
                 
                 // Update port and host if they have changed
                 if (data.port) {{
                     const portElement = document.getElementById('port-display');
                     if (portElement) portElement.textContent = data.port;
                 }}
                 
                 if (data.host) {{
                     const hostElement = document.getElementById('host-display');
                     if (hostElement) hostElement.textContent = data.host;
                 }}
                 
                 showAlert('Status refreshed successfully!', 'success');
             }} catch (error) {{
                 showAlert('Failed to refresh status', 'error');
             }}
         }}
        
                 // ===== COPY FUNCTIONS =====
         function copyShutdownUrl() {{
             const url = `http://${{window.location.hostname}}:${{window.location.port}}/shutdown?key=${{document.getElementById('shutdownKey').value}}`;
             navigator.clipboard.writeText(url).then(() => {{
                 showAlert('Shutdown URL copied to clipboard! This URL includes your current password.', 'success');
             }}).catch(() => {{
                 // Fallback for old browsers
                 const textArea = document.createElement('textarea');
                 textArea.value = url;
                 document.body.appendChild(textArea);
                 textArea.select();
                 document.execCommand('copy');
                 document.body.removeChild(textArea);
                 showAlert('Shutdown URL copied to clipboard!', 'success');
             }});
         }}
         
         

        
        function copyDashboardUrl() {{
            const url = `http://${{window.location.hostname}}:${{window.location.port}}`;
            navigator.clipboard.writeText(url).then(() => {{
                showAlert('Dashboard URL copied to clipboard! Password required for shutdown actions.', 'success');
            }}).catch(() => {{
                // Fallback for old browsers
                const textArea = document.createElement('textarea');
                textArea.value = url;
                document.body.appendChild(textArea);
                textArea.select();
                document.execCommand('copy');
                document.body.removeChild(textArea);
                showAlert('Dashboard URL copied to clipboard!', 'success');
            }});
        }}
        
        // ===== SERVER TEST =====
        async function testServer() {{
            try {{
                const response = await fetch('/api/test');
                const data = await response.json();
                
                if (response.ok) {{
                    showAlert(data.message, 'success');
                }} else {{
                    showAlert(data.message, 'error');
                }}
            }} catch (error) {{
                showAlert('Server test failed: ' + error.message, 'error');
            }}
        }}
        
                 // ===== STARTUP STATUS SYNCHRONIZATION =====
         async function syncStartupStatus() {{
             try {{
                 const key = document.getElementById('shutdownKey').value;
                 const response = await fetch(`/api/status?key=${{key}}`);
                 const data = await response.json();
                 if (response.ok && data.run_on_startup !== undefined) {{
                     updateStartupStatus(data.run_on_startup);
                     console.log('Startup status synced:', data.run_on_startup);
                 }}
             }} catch (error) {{
                 console.log('Could not sync startup status:', error);
             }}
         }}
        
                 // Update startup status display
         function updateStartupStatus(isInStartup) {{
             // Use unique IDs to avoid confusion
             const statusElement = document.getElementById('startup-status-indicator');
             const statusText = document.getElementById('startup-status-text');
             
             if (statusElement && statusText) {{
                 console.log('Updating startup status to:', isInStartup);
                 if (isInStartup) {{
                     statusElement.className = 'status-indicator status-running';
                     statusText.textContent = 'Added to startup';
                 }} else {{
                     statusElement.className = 'status-indicator status-stopped';
                     statusText.textContent = 'Not in startup';
                 }}
             }} else {{
                 console.log('Startup status elements not found');
             }}
         }}
    </script>
</body>
</html>";
        }

        private string GenerateLoginHtml(string error = "")
        {
            return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Remote Shutdown Server - Login</title>
    <link rel=""icon"" type=""image/x-icon"" href=""/favicon.ico"">
    <style>
        /* ===== MAIN STYLES - Remote Shutdown Server ===== */
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            color: #333;
        }}
        
        .container {{
            max-width: 1200px;
            margin: 0 auto;
            padding: 20px;
        }}
        
        .header {{
            text-align: center;
            margin-bottom: 30px;
            color: white;
        }}
        
        .header h1 {{
            font-size: 2.5rem;
            margin-bottom: 10px;
            text-shadow: 2px 2px 4px rgba(0,0,0,0.3);
        }}
        
        .header p {{
            font-size: 1.1rem;
            opacity: 0.9;
        }}
        
        .btn {{
            background: linear-gradient(45deg, #667eea, #764ba2);
            color: white;
            border: none;
            padding: 12px 24px;
            border-radius: 25px;
            cursor: pointer;
            font-size: 1rem;
            font-weight: 500;
            transition: all 0.3s ease;
            margin: 5px;
            text-decoration: none;
            display: inline-block;
        }}
        
        .btn:hover {{
            transform: translateY(-2px);
            box-shadow: 0 5px 15px rgba(0,0,0,0.2);
        }}
        
        .btn-success {{
            background: linear-gradient(45deg, #4CAF50, #45a049);
        }}
        
        .btn-danger {{
            background: linear-gradient(45deg, #f44336, #da190b);
        }}
        
        .form-group {{
            margin-bottom: 20px;
        }}
        
        .form-group label {{
            display: block;
            margin-bottom: 5px;
            font-weight: 500;
            color: #555;
        }}
        
        .form-group input {{
            width: 100%;
            padding: 10px;
            border: 2px solid #e0e0e0;
            border-radius: 8px;
            font-size: 1rem;
            transition: border-color 0.3s ease;
        }}
        
        .form-group input:focus {{
            outline: none;
            border-color: #667eea;
        }}
        
        .alert {{
            padding: 15px;
            border-radius: 8px;
            margin-bottom: 20px;
            display: none;
        }}
        
        .alert-success {{
            background-color: #d4edda;
            color: #155724;
            border: 1px solid #c3e6cb;
        }}
        
        .alert-error {{
            background-color: #f8d7da;
            color: #721c24;
            border: 1px solid #f5c6cb;
        }}
        
        /* ===== LOGIN PAGE SPECIFIC STYLES ===== */
        .login-container {{
            display: flex;
            justify-content: center;
            align-items: center;
            min-height: 100vh;
            padding: 20px;
        }}
        
        .login-card {{
            background: white;
            border-radius: 20px;
            padding: 40px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.1);
            width: 100%;
            max-width: 400px;
            text-align: center;
        }}
        
        .login-card h2 {{
            color: #667eea;
            margin-bottom: 30px;
            font-size: 2rem;
        }}
        
        .login-card .form-group {{
            margin-bottom: 25px;
            text-align: left;
        }}
        
        .login-card .btn {{
            width: 100%;
            padding: 15px;
            font-size: 1.1rem;
            margin-top: 10px;
        }}
        
        .login-info {{
            margin-top: 20px;
            padding: 15px;
            background: #f8f9fa;
            border-radius: 10px;
            font-size: 0.9rem;
            color: #666;
        }}
        
        .login-info strong {{
            color: #667eea;
        }}
        
        /* Responsive Design */
        @media (max-width: 768px) {{
            .login-container {{
                padding: 10px;
            }}
            
            .login-card {{
                padding: 30px 20px;
            }}
            
            .login-card h2 {{
                font-size: 1.8rem;
            }}
        }}
    </style>
</head>
<body>
    <div class=""login-container"">
        <div class=""login-card"">
            <h2>🔒 Login Required</h2>
            
            {(error != "" ? $@"<div class=""alert alert-error"" id=""errorAlert"">{error}</div>" : "")}
            
            <form method=""post"">
                <div class=""form-group"">
                    <label for=""password"">Password:</label>
                    <input type=""password"" id=""password"" name=""password"" placeholder=""Enter your password"" required>
                </div>
                
                <button type=""submit"" class=""btn btn-success"">🚀 Login</button>
            </form>
            
            <div class=""login-info"">
                <strong>Default Password:</strong> 1234<br>
                <strong>Access:</strong> Dashboard and remote control features
            </div>
        </div>
    </div>

    <script>
        /* ===== LOGIN PAGE JAVASCRIPT FUNCTIONS ===== */
        document.addEventListener('DOMContentLoaded', function() {{
            // Focus on the first input when the page loads
            const passwordInput = document.getElementById('password');
            if (passwordInput) {{
                passwordInput.focus();
            }}
        }});
        
        // ===== ENTER KEY SUPPORT =====
        document.getElementById('password').addEventListener('keypress', function(event) {{
            if (event.key === 'Enter') {{
                event.target.form.submit();
            }}
        }});
    </script>
</body>
</html>";
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
            var host = config?.Host ?? "127.0.0.1";
            
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
                    // Try P/Invoke directly first
                    SendMessage(-1, 0x0112, 0xF170, 2);
                    ShowNotification("Remote Shutdown Server", "Monitor closed successfully! ✅");
                }
                catch (Exception)
                {
                    try
                    {
                        // Fallback: use Windows command to close monitor
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

        // P/Invoke to close the monitor
        [DllImport("user32.dll")]
        private static extern int SendMessage(int hWnd, int hMsg, int wParam, int lParam);

        private void ShowAbout()
        {
            var aboutForm = new Form
            {
                Text = "About - Remote Shutdown Server",
                Size = new Size(700, 500),
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                Icon = trayIcon?.Icon,
                BackColor = Color.White
            };

            // Header with gradient
            var headerPanel = new Panel
            {
                Height = 80,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(102, 126, 234)
            };

            var titleLabel = new Label
            {
                Text = "🖥️ Remote Shutdown Server",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(20, 15)
            };

            var versionLabel = new Label
            {
                Text = "Versions 2.1.0",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 200, 255),
                AutoSize = true,
                Location = new Point(20, 45)
            };

            headerPanel.Controls.AddRange(new Control[] { titleLabel, versionLabel });

            // Main content panel with scroll
            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(30),
                AutoScroll = true,
                BackColor = Color.White
            };

            // Explicitly set scroll
            mainPanel.HorizontalScroll.Enabled = false;
            mainPanel.HorizontalScroll.Visible = false;
            mainPanel.VerticalScroll.Enabled = true;
            mainPanel.VerticalScroll.Visible = true;

            int yPosition = 0;

            var descriptionLabel = new Label
            {
                Text = "Application for remote control of the computer via web interface and system tray.",
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.FromArgb(60, 60, 60),
                AutoSize = true,
                Location = new Point(0, yPosition),
                MaximumSize = new Size(620, 0)
            };
            yPosition += 50;

            var featuresLabel = new Label
            {
                Text = "🚀 Main Features",
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = Color.FromArgb(102, 126, 234),
                AutoSize = true,
                Location = new Point(0, yPosition)
            };
            yPosition += 50;

            var featuresList = new Label
            {
                Text = "• Shutdown\n" +
                       "• Close Monitor from system tray\n" +
                       "• Modern and responsive web dashboard\n" +
                       "• Support iPhone Shortcuts\n" +
                       "• Automatic IP and port configuration\n" +
                       "• Windows notifications\n" +
                       "• Customizable password authentication\n" +
                       "• Windows automatic startup",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(40, 40, 40),
                AutoSize = true,
                Location = new Point(0, yPosition),
                MaximumSize = new Size(620, 0)
            };
            yPosition += 150;

            var techLabel = new Label
            {
                Text = "⚙️ Technologies Used",
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = Color.FromArgb(102, 126, 234),
                AutoSize = true,
                Location = new Point(0, yPosition)
            };
            yPosition += 50;

            var techList = new Label
            {
                Text = "• .NET 8.0 - Main framework\n" +
                       "• ASP.NET Core - Web server\n" +
                       "• Windows Forms - Desktop interface\n" +
                       "• C# - Programming language",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(40, 40, 40),
                AutoSize = true,
                Location = new Point(0, yPosition),
                MaximumSize = new Size(620, 0)
            };
            yPosition += 100;

            var infoLabel = new Label
            {
                Text = "ℹ️ Additional Information",
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = Color.FromArgb(102, 126, 234),
                AutoSize = true,
                Location = new Point(0, yPosition)
            };
            yPosition += 50;

            var infoList = new Label
            {
                Text = "• Compatibility: Windows 10/11\n" +
                       "• Requirements: .NET 8.0 or later\n" +
                       "• License: Open Source\n" +
                       "• Developed with ❤️ in C#",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(40, 40, 40),
                AutoSize = true,
                Location = new Point(0, yPosition),
                MaximumSize = new Size(620, 0)
            };
            yPosition += 100;

            var madeByLabel = new Label
            {
                Text = "Made by HexCode",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(102, 126, 234),
                AutoSize = true,
                Location = new Point(0, yPosition),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Status panel with colored background
            var statusPanel = new Panel
            {
                Height = 70,
                Dock = DockStyle.Bottom,
                BackColor = Color.FromArgb(248, 249, 250),
                Padding = new Padding(20)
            };

            var statusLabel = new Label
            {
                Text = $"📊 Status: {(serverRunning ? "🟢 Running" : "🔴 Stopped")} | Port: {config?.Port ?? 5000} | IP: {GetLocalIP()}",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = serverRunning ? Color.Green : Color.Red,
                AutoSize = true,
                Location = new Point(20, 25)
            };

            var closeButton = new Button
            {
                Text = "Close",
                Size = new Size(120, 40),
                Location = new Point(550, 20),
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(102, 126, 234),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };

            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(90, 110, 200);

            statusPanel.Controls.AddRange(new Control[] { statusLabel, closeButton });

            // Add an end element to force scrolling
            var spacerLabel = new Label
            {
                Text = "",
                Height = 50,
                Location = new Point(0, yPosition + 50)
            };

            mainPanel.Controls.AddRange(new Control[]
            {
                descriptionLabel, featuresLabel, featuresList, techLabel, techList, infoLabel, infoList, madeByLabel, spacerLabel
            });

            aboutForm.Controls.AddRange(new Control[] { headerPanel, mainPanel, statusPanel });
            aboutForm.AcceptButton = closeButton;

            aboutForm.ShowDialog();
        }

        private void OpenDonate()
        {
            try
            {
                ShowNotification("Remote Shutdown Server", "The donation page opens.... 💝");

                // PayPal link for donations
                var paypalUrl = "https://ko-fi.com/hexcode64319";

                // Open the link in your default browser
                Process.Start(new ProcessStartInfo
                {
                    FileName = paypalUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowNotification("Remote Shutdown Server", $"Error opening link: {ex.Message}");
            }
        }

        private void RestartServer()
        {
            ShowNotification("Remote Shutdown Server", "Server restarting... 🔄");

            // Stop the web server completely
            serverRunning = false;
            if (webApp != null)
            {
                try
                {
                    webApp.StopAsync().Wait(3000); // Wait a maximum of 3 seconds
                }
                catch { }
            }
            
            Thread.Sleep(2000); // Wait longer to make sure the server has stopped

            // Reload the configuration from the file to get the updated IP
            LoadConfig();

            // Reconfigure the web server with the new configuration
            SetupWebServer();

            // Restart the server.
            StartServer();
            
            ShowNotification("Remote Shutdown Server", "The server has restarted successfully. ✅");
            
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = $"http://localhost:{config?.Port ?? 5000}",
                    UseShellExecute = true
                });
            }
            catch { }
        }


        private void QuitApp()
        {
            ShowNotification("Remote Shutdown Server", "The server has stopped. ❌");

            // Save configuration before exiting
            SaveConfig();
            
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }

            if (webApp != null)
            {
                try
                {
                    webApp.StopAsync().Wait(2000); // Wait a maximum of 2 seconds
                }
                catch { }
            }

            try
            {
                if (File.Exists(lockFile))
                    File.Delete(lockFile);
            }
            catch { }

            // Close the process completely.
            Environment.Exit(0);
        }

        private void StartServer()
        {
            try
            {
                serverRunning = true;
                startTime = DateTime.Now;

                ShowNotification("Remote Shutdown Server", "The server started successfully. ✅");

                if (config?.AutoOpenBrowser == true)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = $"http://localhost:{config.Port}",
                            UseShellExecute = true
                        });
                    }
                    catch { }
                }

                if (webApp != null)
                {
                    Task.Run(async () =>
                    {
                        var port = config?.Port ?? 5000;
                        var localIP = GetLocalIP();
                        Console.WriteLine($"Starting server on: http://0.0.0.0:{port}");
                        Console.WriteLine($"Local IP: {localIP}");
                        Console.WriteLine($"Access from network: http://{localIP}:{port}");

                        // Check if the port is accessible
                        if (IsPortAccessible(port))
                        {
                            Console.WriteLine("✅ Port is accessible from network");
                        }
                        else
                        {
                            Console.WriteLine("⚠️ Port might be blocked by firewall");
                            // Try configuring the firewall
                            EnsureFirewallRule(port);
                        }

                        // Use IPAddress.Any to allow access from the network
                        await webApp.RunAsync();
                    });
                }
            }
            catch
            {
                serverRunning = false;
            }
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
                    // Balloon type with ToolTipIcon.None to use custom icon
                    // The icon comes from trayIcon.Icon (shutdown.ico)
                    trayIcon.ShowBalloonTip(10000, title, message, ToolTipIcon.None);
                }
            }
            catch { }
        }

        private bool CheckStartupStatus()
        {
            if (!OperatingSystem.IsWindows())
                return false;

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
                var startupValue = key?.GetValue("RemoteShutdownServer");
                return startupValue != null && !string.IsNullOrEmpty(startupValue.ToString());
            }
            catch
            {
                return false;
            }
        }

        public void Run()
        {
            StartServer();
            Application.Run(this);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            QuitApp();
            base.OnFormClosing(e);
        }
    }

    public class ServerConfig
    {
        public string SecretKey { get; set; } = "1234";
        public int Port { get; set; } = 5000;
        public string Host { get; set; } = "127.0.0.1";
        public bool RunOnStartup { get; set; } = false;
        public bool AutoOpenBrowser { get; set; } = false;
    }
}


