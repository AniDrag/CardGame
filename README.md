# MartialDie Online – Multiplayer Framework

[![License: Study Only](https://img.shields.io/badge/License-Study%20Only-blue.svg)](LICENSE)

A **Unity‑based** online multiplayer game using **OSC over UDP**.  
This is an online variant of *MartialDie*, built for my own learning experience and as a foundation for fast‑paced, low‑latency game networking.  
**License: For studying use only** – see details below.

---

## Table of Contents

- [What is this?](#what-is-this)
- [How to Build & Run](#how-to-build--run)
  - [Prerequisites](#prerequisites)
  - [Build the Client](#build-the-client)
  - [Run the Server](#run-the-server)
  - [Play the Game](#play-the-game)
- [Code Structure](#code-structure)
  - [Folder Overview](#folder-overview)
  - [Key Files](#key-files)
  - [EventBus](#eventbus)
- [Core Systems](#core-systems)
  - [1. Networking (OSC over UDP)](#1-networking-osc-over-udp)
  - [2. Registration](#2-registration)
  - [3. Lobby & Rooms](#3-lobby--rooms)
  - [4. Timeouts](#4-timeouts)
  - [5. Security (Server side)](#5-security-server-side)
- [Extending the Project](#extending-the-project)
  - [Adding a new OSC message](#adding-a-new-osc-message)
  - [Implementing the actual game scene](#implementing-the-actual-game-scene)
  - [Building the server as a standalone](#building-the-server-as-a-standalone)
- [License & Credits](#license--credits)
- [Acknowledgements](#acknowledgements)

---

## What is this?

This repository contains **both the client and server code** for an online multiplayer game.  
The client is built with **Unity** and communicates with a **standalone server** (C# console app) using the **OSC (Open Sound Control)** protocol over UDP.

### Key features already implemented

- Client registration with unique ID and username
- Room system: create, join, leave, list rooms
- Host authority: only the host can start the game
- Real‑time room updates (player count, host changes, game state)
- Robust error handling – rate limiting, string length caps, automatic disconnection
- Collapsible console (press `F1` in the client) showing all OSC traffic

> **Note:** The actual dice‑based *MartialDie* gameplay is **not yet implemented** – this framework provides the networking backbone.

---

## 🛠️ How to Build & Run

### Prerequisites

| Requirement               | Version / Notes                          |
|---------------------------|------------------------------------------|
| Unity                     | 2021.3 or newer (.NET Standard 2.1)     |
| Server executable         | Built from `OSCServer.cs` (C# console)  |

---

### 1️⃣ Build the Client

1. Open the Unity project.
2. Go to **File → Build Settings**.
3. Select your target platform:
   - **Windows, Mac, Linux** → “PC, Mac & Linux Standalone”
   - **WebGL** → “WebGL” (online version)
4. Click **Build** and choose an output folder.

The built executable will contain the entire UI and networking logic.

---

### Run the Server

The server code (`OSCServer.cs`) is a standard C# console application.  
Compile it using **Visual Studio** or the command line:

```bash
csc OSCServer.cs /reference:OSCTools.dll
```
> **Note:** (Make sure OSCTools.dll or the source files are in the same folder.)

Then run the resulting executable:
  - On Windows: OSCServer.exe
  - On Linux/macOS: mono OSCServer.exe (if Mono is installed)

The server listens on UDP port 55000 by default.
Don't forget to allow inbound UDP traffic on port 55000 in your firewall.
## 🎮 Play the Game

1. Start the server first.
2. Launch the client, enter your username and the server IP (`127.0.0.1` for local tests).
3. Click **Connect** → you will be taken to the lobby.
4. Create or join a room, then the host can start the game (the game scene will load).

> 🌐 The online version will be available later on **Itch.io** (link to be added).

---

## 📁 Code Structure

The Unity project is organised into three main folders:
```text
Assets/
├── Scripts/
│ ├── Client/ # Network client (OSC over UDP)
│ ├── UI/ # All view classes (MainMenu, Lobby, CreateRoom, etc.)
│ ├── Controllers/ # Game state & OSC message handling
│ ├── EventBus/ # Simple in‑memory event system for decoupling
│ └── OSCTools/ # The OSC library (packet parsing, dispatching)
└── Scenes/
├── 0_SC_MainMenu.unity
├── 1_Sc_Lobby.unity
└── 2_Sc_Game.unity # Empty – placeholder for actual gameplay
```


### 📄 Key files

| File | Purpose |
|------|---------|
| `Client.cs` | Singleton that manages the UDP socket, OSC dispatcher, message queuing, timeouts, and connection state. |
| `MainMenuController.cs` | Handles registration flow, listens for `/registered`, then loads the lobby. |
| `LobbyController.cs` | Orchestrates room list display, room creation/joining, and game start. |
| `LobbyView.cs` / `RoomEntryView.cs` | UI for the room list and room entries. |
| `CreateRoomView.cs`, `HostRoomView.cs`, `WaitingForHostView.cs` | Separate panels for each lobby state. |
| `OSCServer.cs` (separate repo) | The backend that tracks clients, rooms, and broadcasts updates. |

### 🧩 EventBus

A lightweight, type‑safe event system (`EventBus<T>`) is used to decouple UI buttons from the controller.  
For example, when the host clicks “Start Game”, `HostRoomView` publishes a `StartGame` event, which `LobbyController` subscribes to.

---

## ⚙️ Core Systems

### 1. Networking (OSC over UDP)

- **Connection** – `Client.Connect()` opens a UDP socket and starts listening.
- **Sending** – `Client.Send(OSCMessageOut)` encodes and transmits OSC packets.
- **Receiving** – Packets are queued on a background thread and processed in `Update()` to avoid threading issues with Unity UI.
- **Listeners** – `Client.AddListener()` registers a callback for a specific OSC address (e.g., `/room_update`).

### 2. Registration

- Client sends `/register "username"`.
- Server replies with `/registered <ID> <username>`.
- The client stores the server‑confirmed username (prevents client‑side spoofing).

### 3. Lobby & Rooms

| Action | OSC message |
|--------|--------------|
| List rooms | `/list_rooms` → server replies with `/room_list` |
| Create room | `/create_room "roomName" pointGoal` |
| Join room | `/join_room "roomName"` |
| Leave room | `/leave_room` |
| Close room (host only) | `/close_room` |
| Start game (host only) | `/start_game` |

Every change triggers a `/room_update` broadcast to all affected clients.

### 4. Timeouts

Each network request can start a timeout coroutine (`Client.StartTimeout()`). If the expected reply does not arrive within the time limit, the timeout callback resets the UI and disconnects if needed.

### 5. Security (Server side)

| Feature | Setting |
|---------|---------|
| Rate limiting | max 50 packets/second per IP |
| Ban policy | 5 violations → 5‑minute ban |
| String length caps | usernames: 12 chars, room names: 20 chars |
| IP bans | stored in memory (no persistence across restarts) |

---

## 🚀 Extending the Project

### Adding a new OSC message

**In server (`OSCServer.cs`)** – register a new handler:

```csharp
dispatcher.AddListener("/my_command", OnMyCommand, ...);
```
In client (e.g., LobbyController) – add a listener:
```csharp
Client.Instance.AddListener("/my_command", OnMyCommand, ...);
```
Send the message:
```csharp
var msg = new OSCMessageOut("/my_command");
msg.AddInt(42);
Client.Instance.Send(msg);
```
