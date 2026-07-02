# Creeper Dice Online

[![License: Study Only](https://img.shields.io/badge/License-Study%20Only-blue.svg)](LICENSE)

**Creeper Dice Online** is a Unity-based multiplayer dice game using a custom C# console server.

The game uses an authoritative server, meaning the server controls the real game state, dice rolls, turns, scores, rooms, and validation.  
Clients only send player actions, such as joining rooms, selecting dice, or choosing whether to bank points or reroll.

**License: For study use only.**

---

## Table of Contents

- [What is this?](#what-is-this)
- [Download the Game Builds](#download-the-game-builds)
- [How to Run](#how-to-run)
  - [1. Start the Server](#1-start-the-server)
  - [2. Start the Game Client](#2-start-the-game-client)
  - [3. Connect to the Server](#3-connect-to-the-server)
  - [4. Playing on the Same Computer](#4-playing-on-the-same-computer)
  - [5. Playing on Multiple Computers](#5-playing-on-multiple-computers)
- [Gameplay Flow](#gameplay-flow)
- [Controls / UI](#controls--ui)
- [Troubleshooting](#troubleshooting)
- [Code Structure](#code-structure)
- [Core Systems](#core-systems)
- [Networking Messages](#networking-messages)
- [Security and Robustness](#security-and-robustness)
- [How to Upload Builds on GitHub](#how-to-upload-builds-on-github)
- [License & Credits](#license--credits)

---

## What is this?

This repository contains the source code and instructions for **Creeper Dice Online**, a multiplayer dice game built in Unity with a separate C# console server.

The client is built with **Unity**.  
The server is a standalone **C# console application**.  
The networking uses **OSC-style messages over TCP sockets**.

### Key features

- Unity client
- Separate C# console server
- TCP socket networking
- OSC message protocol
- Client registration with username and server-assigned ID
- Lobby and room system
- Host-controlled room start
- Server-authoritative dice rolls
- Server-authoritative score and turn validation
- Private turn options sent only to the current player
- Rematch and leave flow after game end
- Heartbeat ping/pong to detect server shutdowns
- Basic malicious client testing tools
- Rate limiting and server-side validation

---

## Download the Game Builds

Download the latest build package from the **GitHub Releases** page.

After downloading, extract the `.zip` file.

Inside, you should have a folder similar to this:

```text
Game Builds/
├── Server Build/
│   └── CreeperDies_Net-Proj.exe
└── Game Build/
    └── CreeperDies.exe
```

The server and the game client are separate programs.

The server must be started first.

---

## How to Run

## 1. Start the Server

Open the extracted folder:

```text
Game Builds/Server Build/
```

Run:

```text
CreeperDies_Net-Proj.exe
```

A console window should open.

The server will display one or more network addresses, for example:

```text
TCP OSC Server running on port 55000

=== Server Network Addresses ===
Listening on all interfaces: 0.0.0.0:55000
Same PC only: 127.0.0.1:55000

Use one of these IPv4 addresses from another device on the same network:
 - 192.168.1.34:55000    (Wi-Fi)
================================
```

Copy one of the displayed IPv4 addresses **without the port**.

Correct:

```text
192.168.1.34
```

Wrong:

```text
192.168.1.34:55000
```

The game client already uses the correct port automatically.

Default port:

```text
55000
```

---

## 2. Start the Game Client

Open the extracted folder:

```text
Game Builds/Game Build/
```

Run:

```text
CreeperDies.exe
```

The main menu should open.

---

## 3. Connect to the Server

In the game client:

1. Enter your player name.
2. Enter the server IPv4 address.
3. Press **Connect**.

Example:

```text
Name: Nik
Server IP: 192.168.1.34
```

If the connection works, you will enter the lobby.

---

## 4. Playing on the Same Computer

If the server and client are running on the same computer, use:

```text
127.0.0.1
```

This address only works on the same machine.

Use this for local testing.

---

## 5. Playing on Multiple Computers

To play on multiple computers:

1. Start the server on one computer.
2. Make sure all players are connected to the same Wi-Fi or local network.
3. Each player starts `CreeperDies.exe`.
4. Each player enters the server computer's displayed IPv4 address.
5. Players can create or join a room from the lobby.
6. The host starts the game.

Example:

```text
Server computer shows:
192.168.1.34:55000

Players type into the client:
192.168.1.34
```

Do not include `:55000` in the client IP field.

---

## Gameplay Flow

1. Start the server.
2. Start one or more clients.
3. Each client connects with a username.
4. Players enter the lobby.
5. A player creates a room and becomes the host.
6. Other players can join the room.
7. The host starts the game.
8. The game scene loads.
9. Players take turns rolling and selecting dice.
10. The server validates all actions.
11. When the game ends, players can choose:
    - **Rematch**
    - **Leave**

If all remaining players press **Rematch**, a new game starts.

If a normal player leaves, they return to the lobby.

If the host leaves during a game, the room closes and all players return to the lobby.

---

## Controls / UI

### Main Menu

| Action | Description |
|---|---|
| Name input | Enter your player name |
| Server IP input | Enter the server IPv4 address |
| Connect | Connect to the server |
| Malicious Tester | Opens the test scene for sending custom network messages |

### Lobby

| Action | Description |
|---|---|
| Create Room | Create a new room |
| Join Room | Join an existing room |
| Leave Room | Leave the current room |
| Start Game | Host starts the game |
| Refresh | Refresh available rooms |
| Disconnect | Disconnect from the server and return to main menu |

### Game

| Action | Description |
|---|---|
| Select Dice | Select a valid dice type during your turn |
| Bank Points | End the turn and keep points if defense is high enough |
| Double Stake / Reroll | Continue rolling with risk |
| Rematch | Vote for a rematch after game end |
| Leave | Leave the game room |

---

## Troubleshooting

### I can only connect with `127.0.0.1`

`127.0.0.1` only works when the server and client are running on the same computer.

For another computer, use the server computer's displayed IPv4 address, for example:

```text
192.168.1.34
```

---

### Other players cannot connect

Check the following:

- The server console is still running.
- Everyone is on the same Wi-Fi or local network.
- You copied the IPv4 address without the port.
- Windows Firewall is not blocking the server executable.
- The server is using port `55000`.
- You are using the correct network adapter address, usually Wi-Fi or Ethernet.
- Do not use VPN, Docker, or virtual adapter addresses unless you know they are correct.

---

### Windows Firewall blocks the server

When starting the server for the first time, Windows may ask for network permission.

Allow access on **Private networks**.

If it still does not work:

1. Open Windows Security.
2. Go to Firewall settings.
3. Allow the server executable through the firewall.
4. Restart the server.
5. Try connecting again.

---

### The game disconnects

If the server closes or crashes, clients should return to the main menu automatically after a few seconds.

Restart the server and reconnect.

---

### The client gets stuck waiting for a reroll / bank prompt

The server may be waiting for a stake response.

The expected client messages are:

```text
/c_stake_answer true
/c_stake_answer false
```

If this happens during development, check that the Roll Again panel is assigned in the GameController inspector.

---

## Code Structure

Example Unity project structure:

```text
Assets/
├── Scripts/
│   ├── Controller/
│   │   ├── Client.cs
│   │   ├── MainMenuController.cs
│   │   ├── LobbyController.cs
│   │   └── GameController.cs
│   │
│   ├── View/
│   │   ├── MainMenuView.cs
│   │   ├── LobbyView.cs
│   │   ├── GameView.cs
│   │   ├── DiceView.cs
│   │   └── UIComponents/
│   │       ├── CreateRoomView.cs
│   │       ├── HostRoomView.cs
│   │       ├── WaitingForHostView.cs
│   │       ├── RollAgainView.cs
│   │       └── GameOverView.cs
│   │
│   ├── EventBus/
│   │   └── EventBus.cs
│   │
│   ├── Networking/
│   │   └── Msg.cs
│   │
│   └── OSCTools/
│
└── Scenes/
    ├── 0_SC_MainMenu.unity
    ├── 1_SC_Lobby.unity
    ├── 2_SC_GameView.unity
    └── 3_SC_MaliciousTester.unity
```

Example server project structure:

```text
Server/
├── Program.cs
├── TcpServer.cs
├── ConsoleCommandHandler.cs
├── LobbyState.cs
├── GameState.cs
├── GameData.cs
├── RoomData.cs
├── ClientInfo.cs
├── Participant.cs
├── Msg.cs
├── OSCTools/
└── NetworkConnections/
```

---

## Core Systems

## 1. Networking

The project uses TCP sockets with OSC-style messages.

The client sends actions to the server.

The server validates actions and sends results back to clients.

Example:

```text
Client sends:
C_SELECT_DICE

Server validates:
Is it this player's turn?
Is this dice type available?
Is this dice type allowed?

Server replies:
S_DICE_SELECTED
S_GAME_STATE
S_DICE_ROLLED
S_TURN_OPTIONS
```

---

## 2. Registration

When a client connects, it sends:

```text
/c_register "username"
```

The server replies with:

```text
/s_registered id username
```

The client stores the server-confirmed ID and username.

This prevents the client from deciding its own ID.

---

## 3. Lobby and Rooms

Players can:

- list rooms
- create rooms
- join rooms
- leave rooms
- close hosted rooms
- start games as host

The room system is controlled by the server.

---

## 4. Game State

The game uses several states/phases, including:

```text
Main Menu
Lobby
Game Scene Loading
NotStarted
Rolling
WaitingForDiceSelection
WaitingForStakeAnswer
TurnEnding
Finished
```

The server stores the real game state.

Clients only display what the server sends.

---

## 5. Heartbeat

The client sends heartbeat pings:

```text
/c_ping
```

The server replies:

```text
/s_pong
```

If the client does not receive a response for a few seconds, it disconnects and returns to the main menu.

This prevents clients from staying stuck when the server closes.

---

## 6. Rematch System

After the game ends, the server keeps the room alive.

Players can press:

```text
Rematch
```

The client sends:

```text
/c_rematch_request
```

The server tracks rematch votes.

When all remaining players vote for rematch, the server resets the game and starts again.

Players can also press:

```text
Leave
```

The client sends:

```text
/c_leave_game
```

If the host leaves during a game, the room closes and all players return to the lobby.

---

## Networking Messages

### General

| Message | Direction | Purpose |
|---|---|---|
| `/c_ping` | Client to Server | Heartbeat ping |
| `/s_pong` | Server to Client | Heartbeat reply |
| `/c_disconnect` | Client to Server | Client disconnect request |
| `/s_disconnect` | Server to Client | Server forces disconnect |
| `/s_shutdown` | Server to Client | Server shutdown warning |
| `/error` | Server to Client | Error message |

---

### Main Menu

| Message | Direction | Purpose |
|---|---|---|
| `/c_register` | Client to Server | Register username |
| `/s_registered` | Server to Client | Confirm ID and username |

---

### Lobby

| Message | Direction | Purpose |
|---|---|---|
| `/c_list_rooms` | Client to Server | Request room list |
| `/s_room_list` | Server to Client | Send room list |
| `/c_create_room` | Client to Server | Create a room |
| `/s_created_room` | Server to Client | Confirm room creation |
| `/c_join_room` | Client to Server | Join room |
| `/s_joined` | Server to Client | Confirm room join |
| `/c_leave_room` | Client to Server | Leave lobby room |
| `/c_close_room` | Client to Server | Host closes room |
| `/s_closed_room` | Server to Client | Room was closed |
| `/c_start_game` | Client to Server | Host starts game |
| `/s_game_started` | Server to Room | Load game scene |

---

### Game

| Message | Direction | Purpose |
|---|---|---|
| `/c_game_scene_ready` | Client to Server | Client game scene loaded |
| `/s_turn_started` | Server to Room | New turn started |
| `/s_dice_rolled` | Server to Room | Show dice roll |
| `/s_turn_options` | Server to Current Player | Send selectable dice |
| `/c_select_dice` | Client to Server | Select dice |
| `/s_dice_selected` | Server to Room | Confirm selected dice |
| `/s_game_state` | Server to Room | Sync scores |
| `/s_game_announcement` | Server to Room | Show announcement |
| `/s_stake_prompt` | Server to Current Player | Ask bank or reroll |
| `/c_stake_answer` | Client to Server | Bank or reroll answer |
| `/s_invalid_move` | Server to Client | Rejected move |
| `/s_game_end` | Server to Room | Game ended |

---

### Rematch

| Message | Direction | Purpose |
|---|---|---|
| `/c_rematch_request` | Client to Server | Vote for rematch |
| `/s_rematch_update` | Server to Room | Update rematch count |
| `/s_rematch_started` | Server to Room | Rematch starts |
| `/c_leave_game` | Client to Server | Leave game room |
| `/s_return_to_lobby` | Server to Client / Room | Return to lobby |

---

## Security and Robustness

The server includes several safety checks.

| Feature | Description |
|---|---|
| Server authority | Server controls dice, turns, points, and game phase |
| Input validation | Server checks username, room name, point goal, dice type, and turn ownership |
| Rate limiting | Limits packet spam per IP |
| Malicious strikes | Repeated invalid behavior can kick a client |
| Heartbeat | Clients detect server shutdowns |
| Scene-ready handshake | Server waits until clients are ready before sending game packets |
| Private turn options | Only the current player receives selectable dice options |
| Room isolation | Multiple rooms can exist separately on one server |

---

## How to Upload Builds on GitHub

Do **not** commit full build folders directly into your normal source-code branch.

Recommended repository structure:

```text
Repository/
├── Assets/
├── Packages/
├── ProjectSettings/
├── README.md
├── LICENSE
└── .gitignore
```

Keep source code in the repository.

For downloadable builds, create a zip file:

```text
CreeperDice_Builds.zip
```

Inside the zip:

```text
Game Builds/
├── Server Build/
│   └── CreeperDies_Net-Proj.exe
└── Game Build/
    └── CreeperDies.exe
```

Upload this zip as a **GitHub Release**.

---

## GitHub Release Steps

1. Go to your GitHub repository.
2. Click **Releases**.
3. Click **Create a new release**.
4. Add a tag, for example:

```text
v1.0.0
```

5. Add a release title:

```text
Creeper Dice Build v1.0.0
```

6. Upload:

```text
CreeperDice_Builds.zip
```

7. Publish the release.

Players can then download the build from the Releases page.

---

## Recommended Download Text

```markdown
## Download Build

Download the latest build from the **Releases** page.

Extract `CreeperDice_Builds.zip`.

Start the server first:

`Server Build/CreeperDies_Net-Proj.exe`

Then start the game client:

`Game Build/CreeperDies.exe`

Copy the IPv4 address shown in the server console and enter it into the game client without the port.
```

---

## License & Credits

OSCTools and networking handout tools: course / study resources.

Rest of the project code:

```text
© Nik Oblak / AniDrag
```

This project is for study use only.

You may not redistribute, sell, or use this code in commercial products without explicit permission.
