# Battle System Documentation

**Godot 4 C# Hex-Based Tactical RPG Battle System**

## System Overview

This is a **turn-based tactical battle system** built in Godot 4 with C#. 

### Key Features
- Hex-based grid combat with offset coordinates
- Initiative-based turn order
- Flexible action system (skills/items/movement/dialogue)
- 5 AI behavior types for NPCs
- Dialogic integration for story sequences
- Data-driven configuration via JSON
- Modular, maintainable architecture

### Design Principles

1. **Separation of Concerns** - Each manager has one clear responsibility
2. **Data-Driven** - Battle setup defined in JSON files
3. **Signal-Based Communication** - Loose coupling via events
4. **Pure Math Engine** - HexGrid does calculations, not game logic
5. **Context-Aware Input** - CentralInputManager routes by active UI

## Current Implementation Status

### ✅ Fully Implemented
- Core battle loop
- Turn management with initiative
- Hex grid math and pathfinding
- Action execution (skills, moves, talk, items)
- NPC AI with 5 behavior patterns
- Visual novel integration
- Scene sequencing system
- Configuration loading

### 🚧 Partially Implemented
- **Inventory System** - Framework exists, needs battle integration testing
- **Battle Results** - Hooks present, UI screen needed

### ⬜ Not Yet Implemented
- Main menu
- Status/party screen
- Settings menu
- Save/load system

See [[TODO & Roadmap]] for detailed implementation plan.

## Quick Start for Developers

### Running the System
1. Open project in Godot 4
2. Main test scene: `res://Assets/Dialogue/Scenes/BaseBattle.tscn`
3. Battle config: `res://data/battle_config.json`
4. Sequence config: `res://data/story_sequence.json`

### Adding New Features
- New action? See [[Action System]]
- New AI behavior? See [[AI Behaviors]]
- New UI screen? See [[Common Patterns]]

### Key Files
- `BattleManager.cs` - Start here for battle logic
- `HexGrid.cs` - Pure math engine
- `Entity.cs` - Data model for all combatants
- `CentralInputManager.cs` - Input routing
- `battle_config.json` - Defines skills/items/entities

## Architecture Overview

```
CentralInputManager (input router)
        ↓
BattleManager (coordinator)
    ├── BattleStateManager (entity tracking)
    ├── BattleUIController (UI display)
    ├── BattleActionHandler (action execution)
    ├── TurnManager (turn order)
    ├── NPCBehaviorManager (AI decisions)
    └── BattleConfigurationLoader (JSON loading)

HexGrid (pure math)
    ├── HexControls (UI interface)
    └── TileMapLayers (visual display)
```

## Contributing Guidelines

### Code Style
- Follow C# conventions
- Use KISS/DRY principles
- One responsibility per class
- Prefer composition over inheritance
- Get Good
