using CreeperDies_Net_Proj.Model;

class Program
{
    static void Main()
    {
        var server = new TcpServer();
        server.Start(55000);

        // States (they register handlers)
        var lobby = new LobbyState(server);
        var game = new GameState(server);

        var consoleHandler = new ConsoleCommandHandler(server);
        consoleHandler.Start();

        // Main loop (synchronous)
        while (true)
        {
            server.Update();
            Thread.Sleep(15);
        }
    }
}