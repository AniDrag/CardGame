using CreeperDice_Net_Proj;
using CreeperDice_Net_Proj.Model;

class Program
{
    static void Main()
    {
        var server = new TcpServer();
        server.Start(55000);

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