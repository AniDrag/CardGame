using CreeperDice_Net_Proj.Model;
using System.Threading;

/*
 * Program
 *
 * Purpose:
 * This is the entry point of the server application.
 *
 * What it does:
 * - Reads optional command line arguments.
 * - Creates the TcpServer.
 * - Starts the server on the chosen port.
 * - Starts the console command handler.
 * - Runs the main server update loop.
 *
 * Naming rule:
 * This file does not receive OSC directly and does not send OSC directly.
 * Because of that, it does not need On or Send prefixes.
 */

class Program
{
    /*
     * Main
     *
     * Purpose:
     * This is where the server application starts.
     *
     * Data received:
     * args = command line arguments.
     *
     * Supported argument:
     * --port <number>
     *
     * Example:
     * CreeperDiceServer.exe --port 55001
     *
     * If no port is given:
     * The server uses port 55000.
     */
    static void Main(string[] args)
    {
        int port = 55000;

        /*
         * Reads command line arguments.
         *
         * If it finds "--port" and another value after it,
         * it tries to parse that value into the port variable.
         *
         * Example:
         * args[0] = "--port"
         * args[1] = "55001"
         */
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--port" && i + 1 < args.Length)
                int.TryParse(args[i + 1], out port);
        }

        /*
         * Creates the main server object.
         *
         * TcpServer owns:
         * - TCP listener
         * - client registry
         * - room registry
         * - OSC dispatcher
         * - lobby state
         * - game state
         */
        var server = new TcpServer();

        /*
         * Starts listening for TCP clients on the selected port.
         */
        server.Start(port);

        /*
         * Starts console commands.
         *
         * This lets the server operator type commands while the server is running.
         * Example uses could be listing players, listing rooms, kicking players,
         * or sending server messages.
         */
        var consoleHandler = new ConsoleCommandHandler(server);
        consoleHandler.Start();

        /*
         * Main server loop.
         *
         * This loop keeps the server alive.
         *
         * Each loop:
         * - accepts new clients
         * - reads incoming packets
         * - dispatches OSC messages
         * - cleans dead connections
         * - updates game timeout checks
         *
         * Thread.Sleep(40):
         * Waits 40 milliseconds between updates.
         * This is about 25 updates per second.
         */
        while (true)
        {
            server.Update();
            Thread.Sleep(40);
        }
    }
}