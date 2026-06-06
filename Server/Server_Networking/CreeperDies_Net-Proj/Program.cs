using CreeperDice_Net_Proj;
using CreeperDice_Net_Proj.Model;

class Program
{
    static void Main(string[] args) 
    {
        int port = 55000;             // default

        // my Helper server starter uses this by pasing a port num here and it starts a server on a set port. Not realy used too much but just for testing here ok
        //Cleare this before the reveiew
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--port" && i + 1 < args.Length)
                int.TryParse(args[i + 1], out port);
        }

        var server = new TcpServer();
        server.Start(port);

        var consoleHandler = new ConsoleCommandHandler(server);
        consoleHandler.Start();

        // Main loop (synchronous)
        while (true)
        {
            server.Update();
            Thread.Sleep(40);
        }
    }
}