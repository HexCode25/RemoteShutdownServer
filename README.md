# 🖥️ Remote Shutdown Server

A powerful Windows application that allows you to remotely control your computer through a modern web interface and system tray. Built with .NET 8.0 and ASP.NET Core, this application provides secure remote shutdown, and other power management features.

## ✨ Features

### 🚀 Core Functionality
- **Remote Shutdown** - Turn off your computer from anywhere
- **Monitor Control** - Turn off your monitor from system tray

### 🌐 Web Interface
- **Modern Dashboard** - Beautiful, responsive web interface
- **Real-time Status** - Live server status and uptime tracking
- **Configuration Panel** - Easy server configuration
- **Mobile Friendly** - Works perfectly on phones and tablets
- **iPhone Shortcuts Support** - Create shortcuts for instant control

### 🔒 Security
- **Password Protection** - Customizable authentication
- **Encrypted Configuration** - Secure config file storage
- **Network Access Control** - Configurable port and IP settings

### 🎯 System Integration
- **System Tray** - Runs silently in the background
- **Windows Notifications** - Toast notifications for all actions
- **Auto Startup** - Optional Windows startup integration
- **Firewall Configuration** - Automatic firewall rule creation

## 🚀 Quick Start

### Prerequisites
- **Windows 10/11**
- **.NET 8.0 Runtime** (included in self-contained builds)

### Installation
1. Download the latest release from the [Releases](https://github.com/HexCode25/RemoteShutdownServer/releases/download/v2.1.0/RemoteShutdownServer-Setup.exe) page
2. Install
3. Run `RemoteShutdownServer.exe`

### First Run
1. The application will start in the system tray
2. Right-click the tray icon and select "🖥️ Dashboard"
3. Login with the default password: `1234`
4. Configure your settings in the dashboard

## 📱 Usage

### Web Dashboard
Access the dashboard at `http://localhost:5000` (or your configured port)

**Default Login:**
- Password: `1234`

### System Tray Menu
Right-click the system tray icon for quick access to:
- 🖥️ **Dashboard** - Open web interface
- 🌐 **Server Status** - View server information
- 💥 **Shutdown PC** - Immediate shutdown
- 🖥️ **Close Monitor** - Turn off monitor
- ℹ️ **About** - Application information
- 💝 **Donate** - Support the project
- 🔄 **Restart Server** - Restart the web server
- ❌ **Exit** - Close the application

### Remote Access URLs
Once configured, you can control your computer using these URLs:

```
Shutdown: http://[YOUR_IP]:[PORT]/shutdown?key=[PASSWORD]
```

### iPhone Shortcuts Setup
1. Open **Shortcuts** app on your iPhone
2. Tap **"+"** to create a new shortcut
3. Search and add **"Get Contents of URL"** action
4. Use the shutdown URL from your dashboard
5. Set method to **"GET"**
6. Add **"Show Result"** action for confirmation
7. Save as **"Shutdown PC"**

## ⚙️ Configuration

### Server Settings
- **Port**: Default 5000 (configurable)
- **Password**: Default "1234" (change immediately)
- **Auto Startup**: Optional Windows startup integration
- **IP Detection**: Automatic local network IP detection

### Configuration File
Settings are stored in `shutdown_server_config.json` with encryption for security.

## 🔧 API Endpoints

### Public Endpoints
- `GET /` - Redirects to login page
- `GET /login` - Login page
- `POST /login` - Authentication
- `GET /favicon.ico` - Application icon

### Authenticated Endpoints
- `GET /shutdown?key=[PASSWORD]` - Shutdown computer

### API Endpoints
- `POST /api/shutdown` - Shutdown via API
- `GET /api/status?key=[PASSWORD]` - Server status
- `POST /api/config` - Update configuration
- `POST /api/startup` - Manage Windows startup
- `GET /api/test` - Server connectivity test

## 🛠️ Development

### Building from Source
```bash
# Clone the repository
git clone https://github.com/your-repo/remote-shutdown-server.git
cd remote-shutdown-server

# Restore dependencies
dotnet restore

# Build the application
dotnet build

# Run the application
dotnet run
```

### Project Structure
```
RemoteShutdownServer/
├── RemoteShutdownServer.cs          # Main application file
├── RemoteShutdownServer.csproj     # Project configuration
├── shutdown.ico                     # Application icon
├── CHANGELOG.md                     # Version history
└── README.md                        # This file
```

### Technologies Used
- **.NET 8.0** - Main framework
- **ASP.NET Core** - Web server
- **Windows Forms** - Desktop interface
- **C#** - Programming language

## 🔒 Security Considerations

### Network Security
- Change the default password immediately
- Use a strong, unique password
- Consider firewall rules for your network
- The application binds to all network interfaces (`0.0.0.0`)

### Best Practices
- Regularly update the application
- Monitor access logs
- Use HTTPS in production environments (future feature)
- Keep your system updated

## 🐛 Troubleshooting

### Common Issues

**Server won't start:**
- Check if port 5000 is available
- Run as administrator if needed
- Check Windows Firewall settings

**Can't access from phone:**
- Ensure both devices are on the same network
- Check the IP address in the dashboard
- Verify firewall allows the port

**Monitor won't turn off:**
- This feature is Windows-only
- Requires proper permissions
- Try running as administrator

### Logs and Debugging
- Check the console output for error messages
- Use the `/api/test` endpoint to verify connectivity
- Check Windows Event Viewer for system errors

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 💝 Support

If you find this project helpful, please consider supporting it:

[![Support via Ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/hexcode64319)

Your support helps us continue development and add new features!

## 📞 Contact

- **Developer**: HexCode
- **Telegram**: [@Hex_Code](https://t.me/Hex_Code)
- **Support**: [Ko-fi](https://ko-fi.com/hexcode64319)

---

**Version**: 2.1.0  
**Last Updated**: September 2025  
**Compatibility**: Windows 10/11, .NET 8.0+
