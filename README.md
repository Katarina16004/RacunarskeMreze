
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
---

## Future Improvements
- Graphical client **UI** (WPF)
- Support for multiple submarine types and advanced rules
- Add a **super move** that can be used only once per game, with configurable behavior
- Per-move **timer**
    - On timeout, a simple bot plays a random valid move instead of the player.
    - On the second bot move, the player receives a warning that one more timeout will end the game for them.
    - On the third timeout, the player is disconnected and considered defeated, while the remaining players continue.
- Log the full course of each game to a file
- **Encrypt messages** between client and server.
  
