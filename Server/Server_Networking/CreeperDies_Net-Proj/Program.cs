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

/*
Q & A session – Program.cs (Server Entry Point)

Q1: What is the purpose of this Program.cs file?
A1: It is the entry point for a standalone C# console server application. It parses command-line arguments,
    starts a TCP server, and runs a main loop to process network updates. This server is used for testing
    and development, separate from the Unity client.

Q2: Why use a synchronous main loop with Thread.Sleep(40) instead of async/await?
A2: This is a simple console application where the server processes updates in a tight loop. Thread.Sleep(40)
    yields the CPU to other processes and limits the update rate to ~25 Hz, which is sufficient for a test
    server. Async/await would add complexity without significant benefit for this single-threaded, non‑UI
    application. The TcpServer's Update() method handles packet processing synchronously.

Q3: Why parse command-line arguments (--port) instead of using a config file?
A3: Command-line arguments allow flexible port configuration during development and testing, without
    modifying code or config files. The default port (55000) matches the client's default (Msg.PORT).

Q4: What is the role of ConsoleCommandHandler?
A4: It likely provides a way for the developer to type commands (e.g., "list rooms", "kick player") into
    the console to interact with the running server. This is useful for debugging and manual testing.
    It runs on a separate thread or listens to console input asynchronously (the code isn't shown).

Q5: Why is the loop infinite (while (true)) without a break condition?
A5: The server is intended to run until the process is terminated (Ctrl+C). A production server might have
    a shutdown command, but this is a simple test server. The loop processes updates and sleeps.

Q6: Why call server.Update() before Thread.Sleep?
A6: The Update() method processes incoming packets, handles disconnections, and updates game state. It must
    be called frequently to keep the server responsive. The order (Update then Sleep) ensures that pending
    work is done before yielding.

Q7: How does this server handle multiple clients concurrently?
A7: The TcpServer likely uses asynchronous socket operations or a thread pool internally. The synchronous
    Update() loop processes all pending events each frame. This is a common pattern for simple servers.

Q8: Why is there no async/await in this code, given that networking is I/O-bound?
A8: The TcpServer itself may use async I/O internally (e.g., BeginAccept, BeginReceive) to handle multiple
    clients efficiently. The main loop remains synchronous because it only calls Update(), which processes
    already-received data. The entry point does not need to be async.

Q9: How does this relate to the Unity client?
A9: This server is the counterpart that listens for OSC messages from the Unity client. The client connects
    to this server's IP and port. The server processes requests (create room, join, start game) and sends
    responses. The Unity client's Client class uses async/await for connection, while the server is simpler
    because it's a console app without UI constraints.

Q10: Why is the code in a separate namespace (CreeperDice_Net_Proj) and uses a model folder?
A10: This suggests the server is part of a larger solution with shared models. Organising code into
     namespaces and folders improves maintainability and separation of concerns. The model folder likely
     contains data classes (RoomDataModel, etc.) used by both client and server.

Q11: Why is the --port argument parsed in a for loop instead of using args parsing libraries?
A11: For a simple test server, manual parsing is lightweight and avoids external dependencies. The loop
    checks for "--port" and reads the next argument. It's sufficient for this use case.

Q12: What happens if the server throws an exception?
A12: The code has no try-catch, so an unhandled exception would crash the process. This is acceptable for a
    test server; in production, you'd add logging and graceful error handling.

Q13: Why does the server not implement a graceful shutdown (e.g., handling Ctrl+C)?
A13: It's a test server. For production, you could add a Console.CancelKeyPress handler to stop the loop,
    close connections, and dispose resources.

Q14: How does the console command handler integrate with the main loop?
A14: The console handler likely runs in a separate thread (or uses asynchronous input reading) so it doesn't
    block the main loop. It may modify server state (e.g., list rooms, send messages) via the server instance
    passed to its constructor.

Q15: Why is port 55000 the default?
A15: This matches the constant Msg.PORT used in the Unity client, ensuring they communicate on the same port
    without configuration. 55000 is an arbitrary, unprivileged port commonly used for custom applications.

Q16: How does the server handle OSC messages specifically?
A16: The TcpServer likely interprets incoming data as OSC packets (using the OSCTools library). It would
    parse messages, route them to handlers, and send responses. This mirrors the client's OSCDispatcher
    and message handling.

Q17: Why is there no async/await even for console input?
A17: Console input is typically read via Console.ReadLine(), which is blocking. The ConsoleCommandHandler
    might use a separate thread to read input without blocking the main loop. Async/await could be used,
    but for a console app, a separate thread is simpler.

Q18: Could this server be converted to use async/await entirely?
A18: Yes, you could rewrite it with async main, await server.StartAsync(), and use async TCP operations.
    However, the current design is adequate for a lightweight test server and aligns with the synchronous
    Update() pattern.

Q19: How does the server handle high loads with Thread.Sleep(40)?
A19: At 25 Hz, it can process updates fairly quickly. For a small test server with a few clients, this is
    fine. For a production server, you'd use non-blocking async I/O and avoid Thread.Sleep, instead using
    async event-driven loops.

Q20: What is the significance of the comment "Cleare this before the reveiew"?
A20: This is a developer note indicating that the port parsing code is only for testing and should be
    removed or cleaned up before a code review. It highlights that this is a temporary debug feature.
*/