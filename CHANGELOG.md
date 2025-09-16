# Remote Shutdown Server - Changelog

## Version 2.1.0

### 🎯 Core Features
- **ASP.NET Core Web Server** with modern dashboard interface
- **Remote control** for shutdown, restart, logout, sleep, hibernate
- **Close Monitor** - Turn off monitor from system tray
- **Authentication** with customizable password
- **System tray** with Windows notifications
- **Automatic configuration** of IP and port
- **iPhone Shortcuts support** for phone control

### 🆕 New Features

#### Close Monitor
- **System tray button** for turning off the monitor
- **Direct P/Invoke** with Windows API for optimal performance
- **PowerShell fallback** if P/Invoke fails
- **Notifications** for action confirmation
- **Windows only** with informative message for other systems

#### About Window
- **About window** with application information
- **Modern design** with attractive colors and fonts
- **Complete feature list**
- **Technical information** (.NET, ASP.NET Core, etc.)
- **Real-time status** (Running/Stopped, Port, IP)
- **System tray button** with visual separator

#### Donate Button
- **Donate button** in system tray with 💝 emoji
- **PayPal link** for donations
- **Automatic opening** in default browser
- **Notifications** for action confirmation
- **Error handling** for failure cases

## Version 2.0.0

### 🎯 Core Features
- **ASP.NET Core Web Server** with modern dashboard interface
- **Remote control** for shutdown, restart, logout, sleep, hibernate
- **Authentication** with customizable password
- **System tray** with Windows notifications
- **Automatic configuration** of IP and port
- **iPhone Shortcuts support** for phone control

### 🚀 Major Improvements

#### Modern Web Dashboard
- **Responsive interface** with modern design and attractive colors
- **Interactive cards** for each functionality
- **Real-time status** with uptime and visual indicators
- **Simplified configuration** with real-time validation
- **Visual feedback** for all actions

#### Enhanced HTML Responses
- **Beautifully formatted HTML pages** for all endpoints
- **Visual confirmations** with emojis and colors for each command
- **Correct Content-Type** for compatibility with all browsers
- **Responsive design** for phone and desktop

#### Advanced Configuration Management
- **Automatic IP detection** on first run
- **Windows Registry synchronization** for startup
- **Configuration encryption** for security
- **Automatic restart** when port is changed
- **Configuration validation** with fallback to default settings

#### iPhone Shortcuts Support
- **Detailed instructions** for configuring shortcuts
- **Pre-generated URLs** with password included
- **Copy buttons** for URLs
- **Test endpoint** for connectivity verification

### 🔧 Technical Improvements

#### Web Server
- **Kestrel** configured for network access (`IPAddress.Any`)
- **CORS** enabled for all origins
- **Static files** for favicon and resources
- **Enhanced error handling** with clear messages

#### API Endpoints
- **GET /shutdown** - Computer shutdown with HTML confirmation
- **POST /api/shutdown** - API for shutdown
- **GET /api/status** - Server status with detailed information
- **POST /api/config** - Server configuration
- **POST /api/startup** - Windows startup management

#### System Tray Features
- **🖥️ Dashboard** - Open web dashboard
- **🌐 Server Status** - Display server status
- **💥 Shutdown PC** - Shutdown computer
- **🖥️ Close Monitor** - Turn off monitor (new in v2.1.0)
- **ℹ️ About** - Open About window (new in v2.1.0)
- **💝 Donate** - Open PayPal donation page (new in v2.1.0)
- **🔄 Restart Server** - Restart server
- **❌ Exit** - Close application

#### Configuration Management
- **Encryption/Decryption** configuration with system-based key
- **Fallback** to default configuration if loading fails
- **Synchronization** with Windows Registry for startup
- **Validation** configuration with clear error messages

#### User Interface
- **System tray** with custom icon (shutdown.ico)
- **Windows notifications** for all actions
- **Context menu** with quick options
- **Web dashboard** with modern and responsive design

### 🐛 Bug Fixes

#### Connectivity Issues
- **Fix IP detection** - Server displays real IP, not manually configured one
- **Fix restart** - Server restarts with updated configuration
- **Fix URL generation** - URLs use correct IP for iPhone
- **Fix CORS** - Correct configuration for network access

#### Configuration Issues
- **Fix host field** - Removed host field from configuration (no longer needed)
- **Fix automatic restart** - Server restarts automatically when port is changed
- **Fix config loading** - Correct configuration loading on restart

#### Interface Issues
- **Fix HTML responses** - Correct HTML responses for all endpoints
- **Fix mobile compatibility** - Responsive design for phone
- **Fix copy functions** - Copy functions for URLs

### 📱 iPhone Shortcuts Support

#### Complete Instructions
1. Open **Shortcuts** app on iPhone
2. Create new shortcut with **"Get Contents of URL"**
3. Use automatically generated URL with password included
4. Set method to **"GET"**
5. Add **"Show Result"** for confirmation
6. Save as **"Shutdown PC"**

#### Available URLs
- **Shutdown**: `http://[IP]:[PORT]/shutdown?key=[PASSWORD]`
- **Restart**: `http://[IP]:[PORT]/restart?key=[PASSWORD]`

### 🔒 Security

#### Authentication
- **Customizable password** (default: 1234)
- **Validation** for all sensitive endpoints
- **Configuration encryption** with system-based key

#### Access
- **CORS** configured for security
- **Input validation** for all parameters
- **Error handling** without exposure of sensitive information

### 📊 Performance

#### Optimizations
- **Async/await** for all I/O operations
- **Background tasks** for server restart
- **Efficient memory usage** with proper disposal
- **Fast startup** with optimized configuration

#### Monitoring
- **Uptime tracking** with real-time display
- **Status monitoring** with visual indicators
- **Error logging** with descriptive messages

### 🎨 Design

#### Modern Interface
- **Gradient backgrounds** with attractive colors
- **Card-based layout** for clear organization
- **Responsive design** for all devices
- **Smooth animations** and transitions

#### Enhanced UX
- **Visual feedback** for all actions
- **Loading states** and confirmations
- **Clear and useful error messages**
- **Intuitive navigation** with descriptive buttons

### 📋 Configuration

#### Default Settings
- **Port**: 5000
- **Password**: 1234 (modifiable)
- **IP**: Automatically detected
- **Startup**: Disabled (modifiable)

#### Configuration Files
- **shutdown_server_config.json** - Main configuration (encrypted)
- **remote_shutdown.lock** - Lock file to prevent multiple instances
- **shutdown.ico** - Icon for system tray

### 🚀 Installation and Running

#### Requirements
- **.NET 8.0** or newer
- **Windows** (for complete functionality)
- **Network access** for phone control

#### Running
1. **dotnet run** - Start the server
2. **Access dashboard** - http://localhost:5000
3. **Configure password** - From Configuration section
4. **Test from phone** - Use displayed URL

### 📈 Previous Versions

#### Version 1.0.0
- Basic shutdown functionality
- Simple interface
- Manual configuration

---

**Current Version: 2.1.0**  
**Date: September 13, 2025**  
**Status: Stable Release**  
**Compatibility: Windows 10/11, .NET 8.0+**
