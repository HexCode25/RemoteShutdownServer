using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace RemoteShutdownServer
{
    public partial class RemoteShutdownServer
    {
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
            webApp = app;
            app.UseStaticFiles();
            app.UseCors();

            app.MapGet("/", (HttpContext context) =>
            {
                context.Response.Redirect("/login");
                return Task.CompletedTask;
            });

            app.MapGet("/login", async (HttpContext context) =>
            {
                context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                context.Response.Headers["Pragma"] = "no-cache";
                context.Response.Headers["Expires"] = "0";
                var html = GenerateLoginHtml();
                await context.Response.WriteAsync(html);
            });

            app.MapPost("/login", async (HttpContext context) =>
            {
                var form = await context.Request.ReadFormAsync();
                var password = form["password"].ToString();

                if (config != null && password == config.SecretKey)
                {
                    context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                    context.Response.Headers["Pragma"] = "no-cache";
                    context.Response.Headers["Expires"] = "0";
                    var html = GenerateDashboardHtml();
                    await context.Response.WriteAsync(html);
                }
                else
                {
                    context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                    context.Response.Headers["Pragma"] = "no-cache";
                    context.Response.Headers["Expires"] = "0";
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
                    host = GetLocalIP(),
                    start_time = startTime.ToString("O"),
                    run_on_startup = IsStartupRegistered()
                };

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(status));
            });

            app.MapGet("/api/test", async (HttpContext context) =>
            {
                var result = new { message = "Server is running perfectly! ✅" };
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(result));
            });

            app.MapPost("/api/config", async (HttpContext context) =>
            {
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

                            _ = Task.Run(async () =>
                            {
                                await Task.Delay(1000);
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

            app.MapPost("/api/startup", async (HttpContext context) =>
            {
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
                            var valueName = "RemoteShutdownServer";
                            var runPath = @"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

                            if (action == "add")
                            {
                                var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                                if (!string.IsNullOrEmpty(exePath))
                                {
                                    var quoted = "\"" + exePath + "\"";
                                    using (var kcu = Registry.CurrentUser.OpenSubKey(runPath, true))
                                    {
                                        kcu?.SetValue(valueName, quoted);
                                    }
                                    try
                                    {
                                        using var klm = Registry.LocalMachine.OpenSubKey(runPath, true);
                                        klm?.SetValue(valueName, quoted);
                                    }
                                    catch { }
                                    if (config != null)
                                        config.RunOnStartup = true;
                                    success = true;
                                    message = "Added to Windows startup successfully! ✅";
                                }
                            }
                            else if (action == "remove")
                            {
                                using (var kcu = Registry.CurrentUser.OpenSubKey(runPath, true))
                                {
                                    kcu?.DeleteValue(valueName, false);
                                }
                                try
                                {
                                    using var klm = Registry.LocalMachine.OpenSubKey(runPath, true);
                                    klm?.DeleteValue(valueName, false);
                                }
                                catch { }
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

        private void StartServer()
        {
            try
            {
                startTime = DateTime.Now;
                var port = config?.Port ?? 5000;
                if (IsPortInUse(port))
                {
                    var choice = MessageBox.Show($"Port {port} is already in use. Would you like to switch to port {port + 1}?", "Port in use", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (choice == DialogResult.Yes && config != null)
                    {
                        config.Port = port + 1;
                        SaveConfig();
                        SetupWebServer();
                    }
                    else
                    {
                        ShowNotification("Remote Shutdown Server", $"Cannot start server on port {port}. ❌");
                        serverRunning = false;
                        return;
                    }
                }

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
                        var localIP = GetLocalIP();
                        Console.WriteLine($"Starting server on: http://0.0.0.0:{port}");
                        Console.WriteLine($"Local IP: {localIP}");
                        Console.WriteLine($"Access from network: http://{localIP}:{port}");

                        if (IsPortAccessible(port))
                        {
                            Console.WriteLine("✅ Port is accessible from network");
                        }
                        else
                        {
                            Console.WriteLine("⚠️ Port might be blocked by firewall");
                            EnsureFirewallRule(port);
                        }

                        try
                        {
                            serverRunning = true;
                            await webApp.RunAsync();
                        }
                        catch
                        {
                            serverRunning = false;
                            Console.WriteLine("Server failed to start.");
                        }
                    });
                }
            }
            catch
            {
                serverRunning = false;
            }
        }

        private bool IsStartupRegistered()
        {
            try
            {
                if (!OperatingSystem.IsWindows()) return false;
                var valueName = "RemoteShutdownServer";
                var runPath = @"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
                var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                if (string.IsNullOrEmpty(exePath)) return false;

                string Normalize(string s) => s.Trim().Trim('"').Replace('/', '\\');
                var target = Normalize(exePath);

                using (var kcu = Registry.CurrentUser.OpenSubKey(runPath, false))
                {
                    var val = kcu?.GetValue(valueName) as string;
                    if (!string.IsNullOrEmpty(val) && string.Equals(Normalize(val), target, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                try
                {
                    using var klm = Registry.LocalMachine.OpenSubKey(runPath, false);
                    var val = klm?.GetValue(valueName) as string;
                    if (!string.IsNullOrEmpty(val) && string.Equals(Normalize(val), target, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch { }

                return false;
            }
            catch { return false; }
        }
    }
}
