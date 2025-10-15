Remote Shutdown Server - Changelog

Version 2.1.1

Bug Fixes
- Web server startup: Assign “webApp = app” after “builder.Build()” to ensure Kestrel actually starts.
- Uptime synchronization: The “/api/status” response now uses “start_time” (snake_case) so the dashboard syncs correctly. Startup flow updated to set “serverRunning” only when “RunAsync()” begins, and reset on failure.
- Host display consistency: Use “GetLocalIP()” for Host in both the web dashboard and the tray status, ensuring a single, correct address is shown.

Reliability Improvements
- Async restart/stop: Run “StopAsync”/restart logic on a background task to avoid blocking the UI thread.
- Port-in-use handling: Detect an occupied port at startup, prompt to switch to the next port, save the config, and rebuild the server automatically.

API Improvements
- “/api/status” fields: Standardized keys to snake_case (“start_time”, “run_on_startup”) for consistency with the frontend.