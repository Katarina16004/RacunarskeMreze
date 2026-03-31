
# Battleship Multiplayer Game

## Project Description
This project is a **client-server multiplayer game** implementation of **Battleship**.  
The server is the central authority that manages player registration, board states, turns, and game results, while clients handle the user’s gameplay interaction.  
Both **UDP** and **TCP** sockets are used for communication.  

- **UDP** – used for fast and lightweight player registration.  
- **TCP** – used for reliable gameplay communication.  

---

## Features
- **Centralized server** that controls:  
  - Player registration  
  - Game initialization  
  - Turns and move validation  
  - Victory conditions  

- **Client gameplay**:  
  - Register via UDP.  
  - After registration, connect via TCP for gameplay.  
  - Place submarines on the grid according to board size.  
  - Take turns to attack opponents by choosing coordinates.  

- **Game mechanics**:  
  - Track hits, misses, and sunk submarines.  
  - Bonus turns for successful hits.  
  - Difficulty levels defined by:  
    - Board size  
    - Allowed consecutive misses  

- **Multiplayer**:  
  - Use of `select()` for socket multiplexing.  
  - Allows multiple players to play simultaneously without blocking.  

---

## Tasks

### 
- **Basic Design**  
  Create a block diagram showing communication between server and clients.  

- **Server Setup**  
  - Input number of players, board size, allowed misses.  
  - Use UDP for player registration.  
  - Provide TCP connection details after registration.  

- **Client Initialization**  
  - Send `PRIJAVA` via UDP.  
  - After TCP connection, send submarine positions based on board size.  

- **Player Data Management**  
  Define a `Player` class with attributes:  
  - Player ID  
  - Misses counter  
  - Submarine positions  
  - Board matrix  

- **Single Turn**  
  - Player selects an opponent and target cell.  
  - Server validates and responds with `MISS` / `HIT` / `SINK`.  
  - Hits grant extra turns.  

###
- **Non-blocking Multiplayer**  
  Implement socket multiplexing (`select`) for handling multiple clients concurrently.  

- **Game State Updates**  
  - Update submarine arrays and boards after each shot.  
  - Ensure players can view the updated board state of selected opponents.  

- **Endgame Conditions**  
  - Eliminate players who exceed misses or lose all submarines.  
  - Last remaining player declared as winner.  

- **Restart Option**  
  - Offer new game to all players after a round.  
  - If declined, close connections gracefully.  

- **Final Diagram**  
  Update the initial block diagram to show full multiplayer interactions and server logic.  


## 🚀 Future Improvements
- Graphical client UI (instead of CLI).  
- Spectator mode.  
- Support for custom submarine types.  
- Enhanced security for connections.  
