namespace CreeperDice_Net_Proj.Model
{
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
        public const string S_CLOSED_ROOM = "/s_closedRoom";                         // -                                         | -                                                                     | N (private)

        
        // Game
        public const string S_YOUR_TURN = "/s_your_turn";                   // String                                    | message                                                               | RoomY (room broadcast)
        public const string S_DICE_ROLLED = "/s_dice_rolled";               // Int, Int[]                                | count, diceIndices                                                    | RoomY (room broadcast)
        public const string S_GAME_STATE = "/s_game_state";                 // Int, Int, (String, Int)[]                 | currentPlayerIndex, playerCount, (name, points)                       | RoomY (room broadcast)
        public const string S_GAME_ANNOUNCEMENT = "/s_game_announcement";   // String                                    | message                                                               | RoomY (room broadcast)
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
}