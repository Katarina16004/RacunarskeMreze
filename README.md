# Network Battleship Game with Cryptographic Protection & Automated Gameplay

A non-blocking client-server implementation of the classic Battleship game.

## Project Description
This project is a **client-server multiplayer game** implementation of **Battleship**.  
The server acts as the central authority that manages player registration, board states, turns, timeouts, and game results, while clients handle the interactive graphical gameplay.  
Both **UDP** and **TCP** sockets are used for communication:
- **UDP** – Used for fast and lightweight player discovery and registration phases.
- **TCP** – Used for reliable, ordered gameplay communication and state synchronization.

---

## Features

### **Centralized Server Authority**
- Manages player registration and lobby states.
- Handles game initialization and dynamic board configurations.
- Validates turns, tracks hits, misses, and sunk submarines.
- Enforces victory conditions and bonus turns for successful hits.
- Implements strict move timers with automated bot fallback and penalty logic.

### **WPF Client Gameplay**
- Modern desktop user interface built with **WPF**.
- UDP-based registration followed by a stable TCP connection for gameplay.
- Interactive fleet placement on customized board dimensions.
- Turn-based coordinate attacks with real-time grid and status updates.

### **Advanced Architecture & Mechanics**
- **Cryptographic Security:** End-to-end **AES-256 encryption** safeguarding sensitive communication streams between clients and the server.
- **Automated Bot Management:** Intelligent bot fallback triggered on player timeouts to maintain seamless game flow.
- **Difficulty Levels:** Configurable parameters including allowed consecutive misses.

---

## Tech Stack
- **Language:** C# (.NET)
- **Framework:** WPF (Windows Presentation Foundation) 
- **Networking:** System.Net.Sockets (Asynchronous TCP & UDP)
- **Security:** System.Security.Cryptography (AES-256)

---

## Getting Started

### Prerequisites
* Visual Studio (2022 or newer) with .NET workload installed.
* Compatible .NET Runtime SDK.

### Running the Application
1. Clone the repository:
   ```bash
   git clone https://github.com/Katarina16004/RacunarskeMreze
   
2. Open the solution file (.sln) in Visual Studio.

3. Set the Server as the startup project and launch it.

4. Launch 2 instances of the WPF Client application.

5. Register via the UI, set up your fleet, and enjoy the game!
