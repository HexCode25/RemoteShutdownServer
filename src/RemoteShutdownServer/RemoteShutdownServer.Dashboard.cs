namespace RemoteShutdownServer
{
    public partial class RemoteShutdownServer
    {
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
    <link rel=""stylesheet"" href=""/css/dashboard.css"">
</head>
<body>
    <img src=""/images/logo.jpg"" alt=""Logo"" class=""logo-corner"" onerror=""this.onerror=null;this.src='data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='64' height='64'%3E%3Crect rx='8' ry='8' width='64' height='64' fill='%23667eea'/%3E%3Ctext x='50%25' y='55%25' dominant-baseline='middle' text-anchor='middle' font-size='28' fill='white'%3ERS%3C/text%3E%3C/svg%3E'"" />
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
                    ✅ Startup status automatically synchronizes with the Windows Registry
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
                        <li>Save as <strong>""Shutdown PC""</strong></li>
                    </ol>
                </div>
            </div>

            <!-- About Hover Widget -->
            <div class=""about-widget"" title=""About"">
                <div class=""about-handle"">ℹ️</div>
                <div class=""about-content"">
                    <h4>Remote Shutdown Server - Changelog</h4>
                    <p><strong>Version 2.1.1</strong></p>
                    <h5>🐛 Bug Fixes</h5>
                    <ul>
                        <li><strong>Web server startup</strong>: Assign <code>webApp = app</code> after <code>builder.Build()</code> to ensure Kestrel actually starts.</li>
                        <li><strong>Uptime synchronization</strong>: Align <code>/api/status</code> response to use <code>start_time</code> (snake_case) so the dashboard syncs correctly. Adjusted server start flow to set <code>serverRunning</code> only when <code>RunAsync()</code> starts and reset on failure.</li>
                        <li><strong>Host display consistency</strong>: Use <code>GetLocalIP()</code> for Host in both the web dashboard and tray status, ensuring a single, correct address is shown.</li>
                    </ul>
                    <h5>🔧 Reliability Improvements</h5>
                    <ul>
                        <li><strong>Async restart/stop</strong>: Run <code>StopAsync</code>/restart logic on a background task to avoid blocking the UI thread.</li>
                        <li><strong>Port-in-use handling</strong>: Detect occupied port at startup, prompt to switch to the next port, save config, and rebuild the server automatically.</li>
                    </ul>
                    <h5>📡 API Improvements</h5>
                    <ul>
                        <li><strong>/api/status fields</strong>: Standardize keys to snake_case (<code>start_time</code>, <code>run_on_startup</code>) for consistency with frontend expectations.</li>
                    </ul>
                    <div style=""margin-top:10px""><a class=""btn"" href=""https://www.paypal.com/donate/?hosted_button_id=ADFQW7RUSFFQY"" target=""_blank"" rel=""noopener"">☕ Donate</a></div>
                </div>
            </div>
        </div>
    </div>

    <script src=""/js/dashboard.js""></script>
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
    <link rel=""stylesheet"" href=""/css/login.css"">
</head>
<body>
    <div class=""login-container"">
        <div class=""login-card"">
            <h2>🔒 Login Required</h2>
            <div class=""alert-error"" id=""errorAlert"" style=""display: {(string.IsNullOrEmpty(error) ? "none" : "block")};""></div>
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

    <script src=""/js/login.js""></script>
</body>
</html>";
        }
    }
}
