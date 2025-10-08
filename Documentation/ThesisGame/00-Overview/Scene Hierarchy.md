
Visual reference for scene tree structures in the battle system.

---

## Overview

The system uses two main scene types:
- **BaseBattle.tscn** - Tactical combat scenes
- **BaseVN.tscn** - Visual novel dialogue scenes

Both share some common UI elements but have different purposes and structures.

---

## BaseBattle.tscn Structure

```
BaseBattle (Node2D)
│
├── HexGrid (Node2D) [DYNAMICALLY LOADED]
│   │   Purpose: Hex-based battlefield grid
│   │   Script: HexGrid.cs
│   │
│   ├── Terrain (TileMapLayer)
│   │   Layer: 0 - Ground tiles
│   │
│   ├── WorldMarker (TileMapLayer)
│   │   Layer: 10 - World objects/decorations
│   │
│   ├── Obstacle (TileMapLayer)
│   │   Layer: 20 - Blocking terrain
│   │
│   ├── Entity (TileMapLayer)
│   │   Layer: 30 - Player, enemies, NPCs
│   │
│   ├── Marker (TileMapLayer)
│   │   Layer: 40 - Range/AOE highlights
│   │
│   ├── Cursor (TileMapLayer)
│   │   Layer: 50 - Targeting cursor
│   │
│   └── HexControls (Node2D) [REPARENTED HERE]
│       Purpose: Hex targeting UI interface
│       Script: HexControls.cs
│
├── CentralInputManager (Node2D)
│   Purpose: Context-aware input routing
│   Script: CentralInputManager.cs
│   Priority: -10000 (processes first)
│
├── BattleManager (Node)
│   Purpose: Central battle coordinator
│   Script: BattleManager.cs
│   Exports:
│   └── configFilePath: "res://data/battle_config.json"
│
└── UI (CanvasLayer)
    Purpose: All battle UI elements
    │
    ├── DialogueContainer (Control)
    │   Purpose: Houses Dialogic UI (hidden during battle)
    │   Visibility: Controlled by CentralInputManager
    │
    ├── MenuActions (MenuControls)
    │   Purpose: Main action menu (Move/Skill/Item/Talk)
    │   Script: MenuControls.cs
    │   Children: 4 predefined buttons
    │   │
    │   ├── VBoxContainer
    │   │   └── Buttons:
    │   │       ├── MoveButton
    │   │       ├── SkillButton
    │   │       ├── ItemButton
    │   │       └── TalkButton
    │
    ├── DynamicMenuRoot (Control)
    │   Purpose: Container for dynamic submenus
    │   │
    │   └── MarginContainer
    │       └── DynamicMenu (MenuControls)
    │           Purpose: Dynamically populated submenu
    │           Script: MenuControls.cs
    │           Content: Managed by BattleManager
    │
    └── InventoryControls (Control) [OPTIONAL]
        Purpose: Inventory screen
        Script: InventoryControls.cs
        Status: 🚧 Partially implemented
```

### Key Points - BaseBattle

**HexGrid Loading**:
- Loaded dynamically in `BattleManager._Ready()`
- Map path from `SceneManager.GetSceneParameter("map")`
- HexControls reparented from UI to HexGrid after load

**TileMapLayer Order** (bottom to top):
1. Terrain - Base ground
2. WorldMarker - Decorations
3. Obstacle - Blocking objects
4. Entity - Characters
5. Marker - Highlights (blue/yellow)
6. Cursor - Targeting cursor (green/red)

**UI Layer Structure**:
- CanvasLayer ensures UI always on top
- MenuActions always visible (until targeting)
- DynamicMenu shown/hidden dynamically
- DialogueContainer hidden during battle

---

## BaseVN.tscn Structure

```
BaseVN (Node2D)
│
└── DialogueContainer (Control)
    Purpose: Houses Dialogic layout for VN scenes
    Children: Added by Dialogic autoload at runtime
    │
    └── [Dialogic Layout Injected Here]
        ├── VN_PortraitLayer (CanvasLayer)
        │   Layer: -1 (behind other UI)
        │   └── Portraits (dynamically added)
        │
        └── VN_TextboxLayer (CanvasLayer)
            └── DialogueControls (Control)
                Script: DialogueControls.cs
                Purpose: Manages portrait positioning
```

### Key Points - BaseVN

**Simplicity**:
- Minimal structure
- Dialogic handles most layout
- DialogueControls adjusts Dialogic's output

**Runtime Injection**:
- Dialogic autoload injects layout when timeline starts
- Portrait layer positioned behind text
- Text panel disabled by DialogueControls

**Timeline Flow**:
1. SceneManager loads BaseVN
2. DialogueControls reads timeline path from SceneManager
3. Calls `Dialogic.start(timelinePath)`
4. Dialogic injects UI structure
5. Timeline plays
6. On end, SceneManager loads next sequence

---

## Scene Transitions

### Story Sequence Flow

```
[Start] → SceneManager
    ↓
Load story_sequence.json
    ↓
sequences[0] = "intro" (type: "vn")
    ↓
Load BaseVN.tscn
    ↓
Start timeline: "intro.dtl"
    ↓ (User reads dialogue)
    ↓
Timeline ends → OnTimelineEnded()
    ↓
sequences[1] = "forest_battle" (type: "battle")
    ↓
Load BaseBattle.tscn
    ↓ Parameters: config, map
    ↓
BattleManager loads map and config
    ↓
Battle plays
    ↓
Battle ends → OnBattleEnded()
    ↓
sequences[2] = "victory" (type: "vn")
    ↓
Load BaseVN.tscn
    ↓
... continues ...
```

### Scene Parameters

**VN Scene Parameters**:
```csharp
sceneParameters["timeline"] = "res://path/to/timeline.dtl"
```

**Battle Scene Parameters**:
```csharp
sceneParameters["battle_config"] = "res://data/battle_config.json"
sceneParameters["map"] = "res://Assets/Maps/Forest.tscn"
```

Accessed via `SceneManager.GetSceneParameter(key)`

---

## Node Relationships

### Parent-Child Rules

**HexGrid Family**:
```
HexGrid (parent)
└── HexControls (child)
    - HexControls uses HexGrid for all math
    - Must be child of HexGrid to access TileMapLayers
```

**UI Family**:
```
UI (CanvasLayer)
├── MenuControls instances
└── Other UI elements
    - All UI must be under CanvasLayer for proper rendering
    - Z-index ordering within CanvasLayer
```

**Input Manager**:
```
CentralInputManager (top-level)
- Does NOT parent any controls
- References controls via discovery/registration
- Routes input based on active context
```

### Script Attachment Points

| Node | Script | Purpose |
|------|--------|---------|
| BaseBattle | None | Container only |
| HexGrid | HexGrid.cs | Hex math engine |
| HexControls | HexControls.cs | Targeting UI |
| CentralInputManager | CentralInputManager.cs | Input routing |
| BattleManager | BattleManager.cs | Battle coordinator |
| MenuActions | MenuControls.cs | Main menu |
| DynamicMenu | MenuControls.cs | Submenu |
| DialogueContainer (Battle) | None | Container only |
| DialogueContainer (VN) | DialogueControls.cs | Dialogic management |

---

## Node Discovery & Registration

### Initialization Order

1. **Scene loads**
2. **`_Ready()` calls** (in tree order):
   - CentralInputManager first (Priority: -10000)
   - BattleManager
   - Other nodes
3. **BattleManager loads HexGrid** (deferred)
4. **CentralInputManager discovers controls**:
   ```csharp
   AutoDiscoverControls()
   ├── Find all MenuControls
   ├── Find all HexControls
   └── Build internal registry
   ```
5. **Components initialize** (in BattleManager)
6. **Battle begins**

### Control Discovery

**MenuControls Discovery**:
```csharp
// Recursive tree traversal
DiscoverControlsRecursively(GetTree().CurrentScene)
    if (node is MenuControls menu)
        RegisterControl(menu)
```

**HexControls Discovery**:
```csharp
// Same recursive search
if (node is HexControls hex)
    RegisterControl(hex)
```

**Manual Registration** (if needed):
```csharp
CentralInputManager.RegisterControl(menuControls)
CentralInputManager.RegisterControl(hexControls)
```

---

## Z-Index Layering

### Visual Stacking Order

```
Top (drawn last)
    ↑
    │ UI Cursor (Z: 2000)
    │ CanvasLayer content (Z: varies)
    │ MenuControls
    │ DynamicMenu
    │
    │ Cursor TileMapLayer (Layer: 50)
    │ Marker TileMapLayer (Layer: 40)
    │ Entity TileMapLayer (Layer: 30)
    │ Obstacle TileMapLayer (Layer: 20)
    │ WorldMarker TileMapLayer (Layer: 10)
    │ Terrain TileMapLayer (Layer: 0)
    ↓
Bottom (drawn first)
```

### CanvasLayer Management

**Battle UI**:
- CanvasLayer (default layer 0)
- All UI elements inside
- Controls use Z-index for ordering

**Dialogic UI** (VN scenes):
- VN_PortraitLayer (Layer: -1) - Behind other UI
- VN_TextboxLayer (Layer: 0) - With other UI
- Adjusted by DialogueControls

---

## Scene File Locations

### Scene Files
```
res://Assets/Dialogue/Scenes/
├── BaseBattle.tscn           # Battle scene template
├── BaseVN.tscn               # VN scene template
└── Maps/
    ├── Forest.tscn           # Forest battle map
    └── Rooftop.tscn          # Rooftop battle map
```

### Map Structure (e.g., Forest.tscn)
```
Forest (Node2D) - Extends HexGrid
├── Terrain (TileMapLayer)
├── WorldMarker (TileMapLayer)
├── Obstacle (TileMapLayer)
├── Entity (TileMapLayer)
├── Marker (TileMapLayer)
└── Cursor (TileMapLayer)
```

Maps are **complete HexGrid scenes** with pre-placed tiles.

---

## Common Scene Patterns

### Adding UI Elements

**To add a new menu**:
1. Add Control node under UI
2. Attach MenuControls script
3. Add buttons as children
4. CentralInputManager auto-discovers it

**To add a new screen**:
1. Create Control node under UI
2. Attach custom script
3. Connect signals to BattleManager
4. Manage visibility in BattleUIController

### Adding Entities to Battle

**Static placement** (in map):
1. Edit map scene (e.g., Forest.tscn)
2. Paint entity tiles on Entity layer
3. BattleStateManager reads tiles on load

**Dynamic placement** (via config):
1. Add EntityDefinition to battle_config.json
2. Set startPosition
3. BattleStateManager creates entity from config

---

## Debugging Scene Issues

### Common Problems

**HexControls not found**:
- Check it's a child of HexGrid (after reparenting)
- Verify HexGrid loaded successfully

**Menu not responding**:
- Check CentralInputManager discovered it
- Verify MenuControls script attached
- Check if menu is active (`.IsActive`)

**UI not visible**:
- Ensure under CanvasLayer
- Check Z-index values
- Verify visibility flags

**Input not working**:
- Check CentralInputManager context
- Verify input actions defined in project settings
- Check process priority (CentralInputManager should be -10000)

### Debug Commands

```gdscript
# Print all discovered controls
CentralInputManager.PrintDiscoveredControls()

# Check current context
print(CentralInputManager.CurrentContext)

# List all children
for child in get_children():
    print(child.name, " - ", child.get_class())
```

---

## Related Documentation

- [[System Architecture]] - How components work together
- [[Component Reference]] - Node script documentation
- [[Input Management]] - CentralInputManager details
- [[Battle Manager]] - Scene initialization flow