namespace RemoteShutdownServer
{
    public class ServerConfig
    {
        public string SecretKey { get; set; } = "1234";
        public int Port { get; set; } = 5000;
        public string Host { get; set; } = "127.0.0.1";
        public bool RunOnStartup { get; set; } = false;
        public bool AutoOpenBrowser { get; set; } = false;
    }
}
