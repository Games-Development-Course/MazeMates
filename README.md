# MazeMates

A cooperative 2D asymmetric maze-navigation game built in Unity 6.

## 🎮 Overview

MazeMates is an asymmetric 2-player puzzle adventure.\
One player is the **Traveller**, physically moving inside the maze; the
other is the **Navigator**, who sees the full map and guides the
Traveller through puzzles, doors, and traps.

The project includes: - Traveller controller (movement, collisions,
camera follow) - Navigator HUD with dynamic puzzle preview - Door system
with puzzle logic - In-game puzzle interactions (draggable pieces &
slots) - Win/Lose scene logic

## 🧩 Game Loop

1.  Player enters the maze scene.
2.  Traveller activates pads, solves puzzles, and opens doors.
3.  Reaching the Exit triggers **WinGame** scene.
4.  Wrong puzzle attempts or failure conditions trigger **GameOver**.

## ✨ Core Features

### Traveller Gameplay

-   Arrow-key movement\
-   Automatic collision blocking\
-   Starts at maze Entrance\
-   Reaching Exit → loads **WinGame** scene

### Navigator HUD

-   Shows puzzle images when relevant\
-   Appears only when Traveller activates puzzle zones\
-   Fully controlled via Door/Puzzle systems

### Door System

Each door supports: - Toggle doors\
- Puzzle doors\
- Pads/pressure plates\
- Target transforms\
- Correct/incorrect validation

### Puzzle System

-   Draggable pieces\
-   Matching slot system\
-   Slot detection by name or ID\
-   Correct placement logic\
-   Progress tracking\
-   Reset on wrong action

## 📁 Project Structure

    Assets/
     ├── Scripts/
     │    ├── InGame/
     │    │     ├── Maze/
     │    │     │     ├── MazeGenerator.cs
     │    │     │     └── MazeTilemapSetup.cs
     │    │     ├── Traveller/
     │    │     │     ├── PlayerMovement1P.cs
     │    │     │     └── CameraFollow.cs
     │    │     ├── Doors/
     │    │     │     ├── DoorController.cs
     │    │     │     ├── PuzzleDoor.cs
     │    │     │     ├── PadTrigger.cs
     │    │     │     └── DoorControllerEditor.cs
     │    │     ├── Puzzles/
     │    │     │     ├── DraggablePiece.cs
     │    │     │     ├── Slot.cs
     │    │     │     └── PuzzleManager.cs
     │    │     └── Game/
     │    │           ├── GameCompletionManager.cs
     │    │           └── WinTrigger.cs
     │    └── UI/
     │          ├── NavigatorHUD.cs
     │          └── SceneButtons.cs
     │
     ├── Scenes/
     │      ├── MazeLevel.unity
     │      ├── WinGame.unity
     │      └── GameOver.unity
     │
     ├── Art/
     │      ├── Tiles/
     │      ├── PuzzleSprites/
     │      └── UI/
     │
     └── Prefabs/
            ├── PlayerTraveller.prefab
            ├── NavigatorHUD.prefab
            ├── Door.prefab
            ├── PuzzleDoor.prefab
            └── Pad.prefab

## 🕹 Controls

**Traveller** - **← ↑ ↓ →** --- Move\
- Collides with maze geometry\
- Activates pads and puzzle zones

**Navigator** - Interacts through HUD puzzle only

## 🙌 Credits

Developed by **Aviv Neeman** with intensive assistance from ChatGPT
during a full week of scripting, debugging, collisions, puzzles, doors,
and HUD implementation.


## Links

https://gamedevteamx.itch.io/coreloopweek4avivn
