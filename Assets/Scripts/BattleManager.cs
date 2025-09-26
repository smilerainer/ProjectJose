// BattleManager.cs - Complete battle system with proper input delegation
using Godot;
using System.Linq;
using System.Collections.Generic;

public partial class BattleManager : Node
{
    [Export] private bool testBattleArrayButtons = true;
    
    private CentralInputManager inputManager;
    private HexGrid hexGrid;
    private MenuControls menuControls;
    private HexControls hexControls;

    private Vector2I playerPosition = new Vector2I(0, 0);
    private Vector2I enemyPosition = new Vector2I(3, 0);
    private bool movementModeActive = false;
    private HashSet<Vector2I> validMoves = new();

    // Testing array functionality
    private readonly string[] battleMenuOptions = { "Fireball", "Ice Thorn", "Shroud" };

    public override void _Ready()
    {
        // Don't disable InputManager for this test - we need it active
        // DisableInputManager();
        
        FindNodes();

        if (hexGrid != null && menuControls != null && hexControls != null)
        {
            SetupInitialState();
            SetupEntities();
            ConnectSignals();
            StartPlayerTurn();
        }
        else
        {
            GD.PrintErr("[Battle] Failed to find required nodes");
            GD.PrintErr($"[Battle] Debug - hexGrid: {hexGrid != null}, menuControls: {menuControls != null}, hexControls: {hexControls != null}, inputManager: {inputManager != null}");
        }
    }

    private void DisableInputManager()
    {
        var inputManager = GetViewport().GetChildren().OfType<CentralInputManager>().FirstOrDefault();
        if (inputManager != null)
        {
            inputManager.SetProcess(false);
            inputManager.SetProcessInput(false);
            inputManager.SetProcessMode(Node.ProcessModeEnum.Disabled);
            GD.Print("[Battle] Disabled InputManager");
        }
    }

    private void FindNodes()
    {
        hexGrid = GetNode<HexGrid>("../HexGrid");
        hexControls = hexGrid?.GetNodeOrNull<HexControls>("HexControls");
        
        // Find MenuControls - this was missing in your version
        menuControls = FindMenuControlsInTree(GetTree().CurrentScene);
        
        // Find CentralInputManager
        inputManager = GetTree().CurrentScene.GetNodeOrNull<CentralInputManager>("CentralInputManager");
        if (inputManager == null)
        {
            inputManager = FindInputManagerInTree(GetTree().CurrentScene);
        }
        
        GD.Print($"[Battle] Found nodes - HexGrid: {hexGrid != null}, HexControls: {hexControls != null}, MenuControls: {menuControls != null}, InputManager: {inputManager != null}");
    }
    
    private CentralInputManager FindInputManagerInTree(Node node)
    {
        if (node is CentralInputManager im) return im;
        foreach (Node child in node.GetChildren())
        {
            var result = FindInputManagerInTree(child);
            if (result != null) return result;
        }
        return null;
    }

    private MenuControls FindMenuControlsInTree(Node node)
    {
        if (node is MenuControls mc) return mc;
        foreach (Node child in node.GetChildren())
        {
            var result = FindMenuControlsInTree(child);
            if (result != null) return result;
        }
        return null;
    }

    private void SetupInitialState()
    {
        if (hexControls != null)
        {
            hexControls.StartUIOnlyMode(playerPosition);
        }
    }

    private void SetupEntities()
    {
        try
        {
            // Place player (yellow) 
            hexGrid.SetTileByCoords(playerPosition, CellLayer.Entity, new Vector2I(0, 0));
            hexGrid.SetOccupied(playerPosition);

            // Place enemy (blue)
            hexGrid.SetTileByCoords(enemyPosition, CellLayer.Entity, new Vector2I(1, 0));
            hexGrid.SetOccupied(enemyPosition);

            GD.Print($"[Battle] Entities placed - Player: {playerPosition}, Enemy: {enemyPosition}");
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"[Battle] Entity setup failed: {e.Message}");
        }
    }

    private void ConnectSignals()
    {
        if (menuControls != null)
            menuControls.ButtonActivated += OnButtonPressed;
        if (hexGrid != null)
            hexGrid.CellSelected += OnCellSelected;
        if (hexControls != null)
        {
            hexControls.MovementCancelled += OnMovementCancelled;
            GD.Print("[Battle] Connected to HexControls.MovementCancelled signal");
        }
    }

    private void StartPlayerTurn()
    {
        if (menuControls != null)
        {
            menuControls.SetActive(true);
        }
        
        GD.Print("[Battle] Player turn started");
        
        // REMOVED: No automatic test - only trigger on Skill button
    }

    private void TestSetBattleMenuArray()
    {
        GD.Print("=== BattleManager Testing Button Array ===");
        GD.Print($"[Battle] Attempting to set battle menu with {battleMenuOptions.Length} options:");
        
        for (int i = 0; i < battleMenuOptions.Length; i++)
        {
            GD.Print($"  [{i}] {battleMenuOptions[i]}");
        }
        
        if (inputManager != null)
        {
            inputManager.SetMenuButtonArray(battleMenuOptions);
            GD.Print("[Battle] ✅ Successfully called inputManager.SetMenuButtonArray()");
        }
        else
        {
            GD.PrintErr("[Battle] ❌ CentralInputManager not found!");
        }
        
        GD.Print("=== BattleManager Array Test Complete ===");
    }

    private void OnButtonPressed(int index, BaseButton button)
    {
        GD.Print($"[Battle] Button {index} pressed - Button text: {button.Name}");
        
        // Check button by text instead of index to be more reliable
        string buttonText = (button.Name).ToString().ToLower();
        
        if (buttonText.Contains("move"))
        {
            if (menuControls != null)
            {
                menuControls.SetActive(false);
            }
            StartMovementMode();
        }
        else if (buttonText.Contains("skill"))
        {
            GD.Print("[Battle] Skill button pressed - showing skill menu");
            ShowSkillMenu();
        }
        else
        {
            GD.Print($"[Battle] Button '{button.Name}' pressed - no action defined");
            // For other buttons, just return to player turn
            StartPlayerTurn();
        }
    }
    
    private void ShowSkillMenu()
    {
        if (inputManager != null)
        {
            GD.Print("[Battle] Opening skill submenu...");
            inputManager.SetMenuButtonArray(battleMenuOptions);
        }
        else
        {
            GD.PrintErr("[Battle] Cannot show skill menu - CentralInputManager not found");
            StartPlayerTurn();
        }
    }

    private void StartMovementMode()
    {
        GD.Print("[Battle] Starting movement mode");
        movementModeActive = true;

        // Get adjacent walkable tiles only
        validMoves = GetAdjacentWalkableTiles(playerPosition);
        GD.Print($"[Battle] Valid moves: {validMoves.Count}");

        UpdateVisuals();

        // Enable HexControls for movement and pass valid moves
        if (hexControls != null)
        {
            hexControls.SetValidMoves(validMoves);
            hexControls.EnterMovementMode();
        }
    }

    private HashSet<Vector2I> GetAdjacentWalkableTiles(Vector2I center)
    {
        var adjacent = new HashSet<Vector2I>();

        // Use HexGrid's corrected GetNeighbors method instead of our own
        var neighbors = hexGrid.GetNeighbors(center);

        GD.Print($"[Battle] Using HexGrid.GetNeighbors for {center}, found {neighbors.Count} walkable neighbors");
        foreach (var neighbor in neighbors)
        {
            GD.Print($"[Battle] Walkable neighbor: {neighbor}");
            adjacent.Add(neighbor);
        }

        return adjacent;
    }

    // Fixed hex neighbor calculation for Godot's offset coordinates
    private List<Vector2I> GetHexNeighbors(Vector2I center)
    {
        var neighbors = new List<Vector2I>();

        // For offset coordinates in Godot (odd-r layout)
        // Even rows (Y % 2 == 0): hexes are shifted left
        // Odd rows (Y % 2 == 1): hexes are shifted right

        Vector2I[] evenRowDirections = {
            new(-1, -1), new(0, -1), // NW, NE
            new(1, 0),              // E
            new(0, 1), new(-1, 1),   // SE, SW
            new(-1, 0)              // W
        };

        Vector2I[] oddRowDirections = {
            new(0, -1), new(1, -1),  // NW, NE
            new(1, 0),              // E
            new(1, 1), new(0, 1),    // SE, SW
            new(-1, 0)              // W
        };

        // Choose direction set based on row parity
        Vector2I[] directions = (center.Y % 2 == 0) ? evenRowDirections : oddRowDirections;

        foreach (var dir in directions)
        {
            neighbors.Add(center + dir);
            GD.Print($"[Battle] Neighbor of {center}: {center + dir}");
        }

        return neighbors;
    }

    private void UpdateVisuals()
    {
        // Clear both layers using the HexGrid's GetLayer method
        var markerLayer = hexGrid.GetLayer(CellLayer.Marker);
        markerLayer?.Clear();

        var cursorLayer = hexGrid.GetLayer(CellLayer.Cursor);
        cursorLayer?.Clear();

        // Draw blue highlights for valid moves on Marker layer
        foreach (var move in validMoves)
        {
            hexGrid.SetTileByCoords(move, CellLayer.Marker, new Vector2I(1, 0)); // Blue
        }

        GD.Print($"[Battle] Updated visuals - highlighted {validMoves.Count} valid moves");
    }

    private void ExecuteMove(Vector2I targetPosition)
    {
        if (!validMoves.Contains(targetPosition))
        {
            GD.Print($"[Battle] Cannot move to {targetPosition} - invalid");
            return;
        }

        GD.Print($"[Battle] Moving player from {playerPosition} to {targetPosition}");

        // Clear old position
        hexGrid.ClearTile(playerPosition, CellLayer.Entity);
        hexGrid.SetOccupied(playerPosition, false);

        // Move to new position
        playerPosition = targetPosition;
        hexGrid.SetTileByCoords(playerPosition, CellLayer.Entity, new Vector2I(0, 0)); // Yellow player
        hexGrid.SetOccupied(playerPosition);

        ExitMovementMode();
    }

    private void ExitMovementMode()
    {
        movementModeActive = false;

        // Clear all highlights using the HexGrid's GetLayer method
        var markerLayer = hexGrid.GetLayer(CellLayer.Marker);
        markerLayer?.Clear();

        var cursorLayer = hexGrid.GetLayer(CellLayer.Cursor);
        cursorLayer?.Clear();

        // Return cursor to player and disable hex input
        if (hexControls != null)
        {
            hexControls.ExitMovementMode(playerPosition);
        }

        GD.Print("[Battle] Exited movement mode");
        StartPlayerTurn(); // This should reactivate the menu
    }

    private void OnMovementCancelled()
    {
        GD.Print("[Battle] Movement cancelled by player");
        ExitMovementMode();
    }

    private void OnCellSelected(Vector2I cell)
    {
        if (!movementModeActive) return;

        if (validMoves.Contains(cell))
        {
            ExecuteMove(cell);
        }
        else
        {
            GD.Print($"[Battle] Invalid click at {cell}");
        }
    }
}