public static class Msg
{
    public const int PORT = 55000;//Universal server port

    #region Timeouts
    // MainMenu
    public const string REGISTER_TIMEOUT_ID = "register";               // Timeout ID for registration

    // Lobby
    public const string CREATE_ROOM_TIMEOUT = "create_room";            // Timeout ID for create room operation
    public const string JOIN_ROOM_TIMEOUT = "join_room";                // Timeout ID for join room operation
    public const string REFRESH_ROOMS_TIMEOUT = "refresh_rooms";        // Timeout ID for room list refresh
    #endregion

    #region Server OSC Messages (Server -> Client)
    // General
    public const string S_DISCONNECT = "/s_disconnect";                 // -                                         | -                                                                     | N (private)
    public const string S_SHUTDOWN = "/s_shutdown";                     // String                                    | reason                                                                | Y (all clients)
    public const string S_SERVER_MESSAGE = "/s_server_message";         // String                                    | message                                                               | Y (all clients)
    public const string S_ERROR = "/error";                             // String                                    | errorMessage                                                          | N (private)

    // MainMenu
    public const string S_REGISTERED = "/s_registered";                 // Int, String                               | id, username                                                          | N (private)

    // Lobby
    public const string S_ROOM_LIST = "/s_room_list";                   // Int, (String, Int, String, Int, Int)[]    | roomCount, (name, goal, host, playerCount, state)                     | N (private)
    public const string S_ROOM_UPDATE = "/s_room_update";               // String, Int, Int, String, Int, Bool       | roomName, currentPlayers, maxPlayers, host, pointGoal, gameStarted    | RoomY (room broadcast)
    public const string S_GAME_STARTED = "/s_game_started";             // -                                         | -                                                                     | RoomY (room broadcast)
    public const string S_CREATED_ROOM = "/s_created_room";             // -                                         | -                                                                     | N (private)
    public const string S_JOINED = "/s_joined";                         // -                                         | -                                                                     | N (private)
    public const string S_CLOSED_ROOM = "/s_closed_room";                         // -                                         | -                                                                     | N (private)


    // Game
    public const string S_YOUR_TURN = "/s_your_turn";                   // String                                    | message                                                               | RoomY (room broadcast)
    public const string S_DICE_ROLLED = "/s_dice_rolled";               // Int, Int[]                                | count, diceIndices                                                    | RoomY (room broadcast)
    public const string S_GAME_STATE = "/s_game_state";                 // Int, Int, (String, Int)[]                 | currentPlayerIndex, playerCount, (name, points)                       | RoomY (room broadcast)
    public const string S_GAME_ANNOUNCEMENT = "/s_game_announcement";   // String                                    | message                                                                  | RoomY (room broadcast)
    public const string S_ROUND_RESULTS = "/s_round_results";           // String                                    | results                                                               | RoomY (room broadcast)
    public const string S_STAKE_PROMPT = "/s_stake_prompt";             // Bool, String                              | canStake, optionalMessage                                             | N (private)
    public const string S_GAME_END = "/s_game_end";                     // String                                    | winnerMessage                                                         | RoomY (room broadcast)
    #endregion

    #region Client OSC Messages (Client -> Server)
    // General
    public const string C_DISCONNECT = "/c_disconnect";                 // -                                         | -                                                                     | N (private)

    // MainMenu
    public const string C_REGISTER = "/c_register";                     // String                                    | username                                                              | N (private)

    // Lobby
    public const string C_LIST_ROOMS = "/c_list_rooms";                 // -                                         | -                                                                     | N (private)
    public const string C_CREATE_ROOM = "/c_create_room";               // String, Int                               | roomName, pointGoal                                                   | N (private)
    public const string C_JOIN_ROOM = "/c_join_room";                   // String                                    | roomName                                                              | N (private)
    public const string C_LEAVE_ROOM = "/c_leave_room";                 // -                                         | -                                                                     | N (private)
    public const string C_CLOSE_ROOM = "/c_close_room";                 // -                                         | -                                                                     | N (private)
    public const string C_START_GAME = "/c_start_game";                 // -                                         | -                                                                     | N (private)

    // Game
    public const string C_SELECT_DICE = "/c_select_dice";               // Int                                       | diceType                                                              | N (private)
    public const string C_STAKE_ANSWER = "/c_stake_answer";             // Bool                                      | cashOut (true=cash, false=stake)                                      | N (private)
    #endregion
}
/*
Q & A session – Msg (OSC Message Constants)

Q1: What is the purpose of the Msg class?
A1: It centralises all OSC message addresses, timeout IDs, and port numbers used by both the client and server. 
    This ensures consistency across the codebase – every message address is defined in one place, reducing typos 
    and making it easy to update the protocol. It acts as the "contract" between client and server.

Q2: Why use const string for message addresses instead of enums or readonly fields?
A2: const strings are compile-time constants. They are inlined by the compiler, which improves performance and 
    ensures they cannot be changed at runtime. This is ideal for fixed OSC addresses. Enums would require 
    conversion to strings at runtime, adding overhead. readonly fields would not be inlined. const is the most 
    efficient and appropriate choice for these values.

Q3: Why are messages prefixed with C_ and S_?
A3: The prefix indicates the direction of the message:
    - C_ (Client) messages are sent from the client to the server (e.g., C_REGISTER, C_CREATE_ROOM).
    - S_ (Server) messages are sent from the server to the client (e.g., S_REGISTERED, S_ROOM_LIST).
    This naming convention makes it immediately clear which side originates the message, improving code readability.

Q4: What do the comments like "String, Int" or "-" in the message definitions mean?
A4: They document the expected OSC arguments for each message. For example, S_REGISTERED takes two arguments: 
    an Int (client ID) followed by a String (username). "-" means no arguments. This serves as inline 
    documentation for developers and helps ensure messages are constructed correctly in code.

Q5: Why are there separate message groups (MainMenu, Lobby, Game)?
A5: Organising messages by their functional area (scene or state) makes the class easier to navigate. It also 
    reflects the flow of the application – different parts of the code use different groups of messages. 
    This modularity helps with maintenance and onboarding new developers.

Q6: What are the timeout IDs (REGISTER_TIMEOUT_ID, CREATE_ROOM_TIMEOUT, etc.) used for?
A6: These are unique string identifiers used with the Client.StartTimeout() and Client.CancelTimeout() methods. 
    They allow the client to associate a timeout with a specific operation. For example, when sending C_CREATE_ROOM, 
    the client starts a timeout with ID "create_room". If the server doesn't respond within the time, the timeout 
    callback fires and handles the failure. The ID ensures that the correct timeout is cancelled when the response 
    arrives (e.g., OnRoomCreated cancels "create_room").

Q7: Why are some messages marked as "private" and others as "Y (all clients)" or "RoomY (room broadcast)"?
A7: This indicates the message's delivery scope, which is important for understanding the protocol:
    - N (private): Sent only to a specific client (e.g., registration reply, error, join confirmation).
    - Y (all clients): Broadcast to every connected client (e.g., server shutdown, server message).
    - RoomY (room broadcast): Sent to all clients that are in a specific room (e.g., game state updates, dice rolls).
    This comment helps developers know how the server should distribute the message.

Q8: How does the port constant (PORT = 55000) relate to the client and server?
A8: Both the client (Unity app) and the server (console app) use the same port number (55000) by default. 
    This ensures they can communicate without requiring manual port configuration. The client's Client.Connect() 
    method uses Msg.PORT if no port is specified, and the server's Program.cs parses "--port" but also falls 
    back to Msg.PORT. Having a single constant guarantees consistency.

Q9: Why are messages like S_JOINED and S_CREATED_ROOM separate, while S_ROOM_UPDATE can also convey changes?
A9: The protocol uses different messages for different events:
    - S_CREATED_ROOM is sent to the host to confirm that the room was created and to trigger the UI transition.
    - S_JOINED is sent to a player who successfully joined a room (non-host) to show the waiting view.
    - S_ROOM_UPDATE is a general broadcast that updates the room information (e.g., participant count, host, 
      gameStarted status) in the lobby list.
    Having separate messages for distinct events makes the code clearer and allows each to carry exactly the data 
    needed, without overloading S_ROOM_UPDATE with too many optional fields.

Q10: Why does C_CLOSE_ROOM and C_START_GAME have no arguments, while C_CREATE_ROOM and C_JOIN_ROOM do?
A10: The arguments reflect the data needed for the operation:
    - C_CREATE_ROOM requires a room name and point goal (two arguments).
    - C_JOIN_ROOM needs the room name to join.
    - C_START_GAME and C_CLOSE_ROOM are commands that act on the current room (which the server already knows 
      from the client's state). No additional data is needed.
    This keeps messages lightweight and prevents redundant data transmission.

Q11: How does the Msg class integrate with the client's OSC dispatcher and server's OSCDispatcher?
A11: The client uses Client.AddListener(Msg.S_REGISTERED, ...) to register callbacks for incoming server messages. 
    The server's OSCDispatcher maps received OSC addresses to handler methods (e.g., OnRegister for Msg.C_REGISTER). 
    By using the constants from Msg, both sides reference the same strings, ensuring they match exactly.

Q12: Why isn't there a separate constant for the IP address or other networking parameters?
A12: The IP address is configurable at runtime (set by the user in the main menu). Unlike the port, which is fixed 
    for the application, the IP can vary (localhost, LAN, or internet). The port is defined as a constant because 
    it's the same for all instances of the game. The IP is passed as a parameter to Client.Connect().

Q13: What is the significance of the "/" prefix in OSC addresses?
A13: OSC (Open Sound Control) specifies that message addresses must start with a forward slash ("/") followed by 
    a string. This is part of the OSC standard. All our message addresses follow this convention, ensuring 
    compatibility with OSC libraries (OSCTools). The constants include the leading slash, so when building a 
    message we can write new OSCMessageOut(Msg.C_REGISTER) directly.

Q14: Why are there comments for each message describing the arguments?
A14: This serves as documentation for developers, especially when debugging or extending the protocol. It clarifies 
    what each message expects or provides, reducing the need to look up the server or client implementation. 
    It also helps ensure that sender and receiver agree on the argument order and types.

Q15: How does the server know which client sent a message for private messages (N)?
A15: The OSC dispatcher passes the IPEndPoint of the sender along with the message. The server uses that endpoint 
    to look up the corresponding client (via GetClientByEndpoint) and processes the message in the context of that 
    client. This allows private messages to be routed correctly.

Q16: Could the Msg class be replaced with a resource file or external config?
A16: It could, but using a static class with const strings is simpler and keeps the code self-contained. 
    External config files would add complexity for little benefit, as OSC addresses rarely change during the 
    application's lifetime. The static class also provides compile?time checking and IntelliSense support in the IDE.

Q17: Why are there no messages for the game's turn logic (e.g., S_DICE_ROLLED) defined with detailed parameter comments?
A17: They are defined, and the comments include the argument types (e.g., S_DICE_ROLLED: Int, Int[]). This follows 
    the same pattern as other messages. The game messages are grouped under the "Game" region.

Q18: How are these message constants used in the client's Debug_SendMessage deserializer?
A18: The deserializer takes a user?provided string (e.g., "/c_create_room /string_MyRoom /int_50") and creates an 
    OSCMessageOut using the address as the first token. The address string is directly compared to these constants 
    (e.g., Msg.C_CREATE_ROOM) in other parts of the code. In Debug_SendMessage, the address is used as?is, so the 
    user must type the exact address (including slashes) from this list.

Q19: Why are timeout IDs separate from message addresses (e.g., CREATE_ROOM_TIMEOUT not equal to C_CREATE_ROOM)?
A19: Timeout IDs are internal identifiers for the client's timeout system. They don't need to match OSC addresses; 
    they can be any unique string. Using a separate constant helps avoid accidental collisions and makes the intent 
    clear (this is a timeout ID, not a network message). It also allows the same timeout ID to be used for 
    operations that involve multiple messages (e.g., registration timeout spans the connect and register steps).

Q20: Is there any reason the port is not also in a separate class or configurable at runtime?
A20: The port could be made configurable, but for a small?scale project, a fixed port simplifies development and 
    deployment. If needed in the future, we could move it to a settings class or command?line argument. 
    The current approach is sufficient for the game's scope.
*/ 