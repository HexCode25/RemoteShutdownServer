using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace RemoteShutdownServer
{
    public partial class RemoteShutdownServer
    {
        private void LoadConfig()
        {
            try
            {
                if (File.Exists(configFile))
                {
                    var fileContent = File.ReadAllText(configFile);
                    config = DecryptConfig(fileContent);

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
    }
}
