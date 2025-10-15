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
}
