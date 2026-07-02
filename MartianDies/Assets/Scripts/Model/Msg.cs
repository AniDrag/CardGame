public static class Msg
{
    public const int PORT = 55000;

    #region Timeouts

    // Main Menu
    public const string REGISTER_TIMEOUT_ID = "register";               // Timeout ID for registration

    // Lobby
    public const string CREATE_ROOM_TIMEOUT = "create_room";            // Timeout ID for create room operation
    public const string JOIN_ROOM_TIMEOUT = "join_room";                // Timeout ID for join room operation
    public const string REFRESH_ROOMS_TIMEOUT = "refresh_rooms";        // Timeout ID for room list refresh

    #endregion

    #region Server OSC Messages - General
    public const string S_PONG = "/s_pong";                             // -                                            | heartbeat reply                                                                           | N
    public const string S_DISCONNECT = "/s_disconnect";                 // String                                       | reason                                                                                    | N (private)
    public const string S_SHUTDOWN = "/s_shutdown";                     // String                                       | reason                                                                                    | Y (all clients)
    public const string S_SERVER_MESSAGE = "/s_server_message";         // String                                       | message                                                                                   | Y / RoomY / N
    public const string S_ERROR = "/error";                             // String                                       | errorMessage                                                                              | N (private)

    #endregion

    #region Server OSC Messages - Main Menu

    public const string S_REGISTERED = "/s_registered";                 // Int, String                                  | id, username                                                                              | N (private)

    #endregion

    #region Server OSC Messages - Lobby

    public const string S_ROOM_LIST = "/s_room_list";                   // Int, (String, Int, String, Int, Int)[]       | roomCount, (roomName, pointGoal, host, playerCount, state)                                | N (private)
    public const string S_ROOM_UPDATE = "/s_room_update";               // String, Int, String, Int, Bool               | roomName, currentPlayers, host, pointGoal, gameStarted                                    | Y (all lobby clients)
    public const string S_CREATED_ROOM = "/s_created_room";             // String, Int, String, Int, Bool               | roomName, currentPlayers, host, pointGoal, gameStarted                                    | N (private to creator)
    public const string S_JOINED = "/s_joined";                         // String, Int, String, Int, Bool               | roomName, currentPlayers, host, pointGoal, gameStarted                                    | N (private to joiner)
    public const string S_CLOSED_ROOM = "/s_closed_room";               // String                                       | roomName                                                                                  | Y (all clients)
    public const string S_GAME_STARTED = "/s_game_started";             // -                                            | -                                                                                         | RoomY (room broadcast)

    #endregion

    #region Server OSC Messages - Game
    public const string S_TURN_STARTED = "/s_turn_started";             // Int, String                                  | currentPlayerId, currentPlayerName                                                        | RoomY (room broadcast)
    public const string S_YOUR_TURN = "/s_your_turn";                   // String                                       | message                                                                                   | RoomY (room broadcast)
    public const string S_DICE_ROLLED = "/s_dice_rolled";               // Int, Int, Int[], Int, Int, Int, Bool         | currentPlayerId, diceCount, diceIndices, turnPoints, defense, attack, doubleStakeActive   | RoomY (room broadcast)
    public const string S_TURN_OPTIONS = "/s_turn_options";             // Int, (Int, Bool)[]                           | selectableCount, (diceType, isSelectable)                                                 | N (private to current player)
    public const string S_DICE_SELECTED = "/s_dice_selected";           // Int, Int, Int, Int, Bool                     | diceType, turnPoints, defense, attack, doubleStakeActive                                  | RoomY (room broadcast)
    public const string S_GAME_STATE = "/s_game_state";                 // Int, Int, (String, Int)[]                    | currentPlayerIndex, playerCount, (playerName, totalPoints)                                | RoomY (room broadcast)
    public const string S_GAME_ANNOUNCEMENT = "/s_game_announcement";   // String                                       | message                                                                                   | RoomY (room broadcast)
    public const string S_ROUND_RESULTS = "/s_round_results";           // String                                       | results                                                                                   | RoomY (room broadcast)
    public const string S_STAKE_PROMPT = "/s_stake_prompt";             // -                                            | -                                                                                         | N (private to current player) 
    public const string S_INVALID_MOVE = "/s_invalid_move";             // String                                       | reason                                                                                    | N (private to client that made invalid move)
    public const string S_GAME_END = "/s_game_end";                     // String                                       | winnerMessage                                                                             | RoomY (room broadcast)
                                                                        // REPLAY / REMATCH
    public const string S_REMATCH_UPDATE = "/s_rematch_update";         // Int, Int                                     | readyCount, neededCount                                                                   | RoomY
    public const string S_REMATCH_STARTED = "/s_rematch_started";       // -                                            | rematch has started                                                                       | RoomY
    public const string S_RETURN_TO_LOBBY = "/s_return_to_lobby";       // String                                       | reason                                                                                    | N / RoomY

    #endregion

    #region Client OSC Messages - General
    public const string C_PING = "/c_ping";                             // -                                            | heartbeat ping                                                                            | N
    public const string C_DISCONNECT = "/c_disconnect";                 // -                                            | -                                                                                         | N (private)

    #endregion

    #region Client OSC Messages - Main Menu

    public const string C_REGISTER = "/c_register";                     // String                                       | username                                                                                  | N (private)

    #endregion

    #region Client OSC Messages - Lobby

    public const string C_LIST_ROOMS = "/c_list_rooms";                 // -                                            | -                                                                                         | N (private)
    public const string C_CREATE_ROOM = "/c_create_room";               // String, Int                                  | roomName, pointGoal                                                                       | N (private)
    public const string C_JOIN_ROOM = "/c_join_room";                   // String                                       | roomName                                                                                  | N (private)
    public const string C_LEAVE_ROOM = "/c_leave_room";                 // -                                            | -                                                                                         | N (private)
    public const string C_CLOSE_ROOM = "/c_close_room";                 // -                                            | -                                                                                         | N (private)
    public const string C_START_GAME = "/c_start_game";                 // -                                            | -                                                                                         | N (private)

    #endregion

    #region Client OSC Messages - Game
    public const string C_GAME_SCENE_READY = "/c_game_scene_ready";     // -                                            | game scene loaded and listeners registered                                                | N (private)
    public const string C_SELECT_DICE = "/c_select_dice";               // Int                                          | diceType                                                                                  | N (private)
    public const string C_STAKE_ANSWER = "/c_stake_answer";             // Bool                                         | doReRollOrDoubleStake                                                                     | N (private)
    public const string C_REMATCH_REQUEST = "/c_rematch_request";       // -                                            | player wants rematch                                                                      | N
    public const string C_LEAVE_GAME = "/c_leave_game";                 // -                                            | player leaves game room                                                                   | N

    #endregion
}