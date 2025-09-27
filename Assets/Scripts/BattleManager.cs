using Godot;
using System.Linq;
using System.Collections.Generic;

public partial class BattleManager : Node
{
    #region Node References
    
    private CentralInputManager inputManager;
    private HexGrid hexGrid;
    private MenuControls menuControls;
    private HexControls hexControls;
    
    #endregion
    
    #region Battle State
    
    private Vector2I playerPosition = new Vector2I(0, 0);
    private Vector2I enemyPosition = new Vector2I(3, 0);
    private bool movementModeActive = false;
    private HashSet<Vector2I> validMoves = new();
    private bool isInSubmenu = false;
    private string currentSubmenuType = "";
    
    #endregion
    
    #region Menu Options
    
    private readonly string[] skillOptions = { "Fireball", "Ice Thorn", "Shroud" };
    private readonly string[] itemOptions = { "Potion", "Ether", "Phoenix Down" };
    private readonly string[] talkOptions = { "Negotiate", "Intimidate", "Flee" };
    
    #endregion
    
    #region Initialization
    
    public override void _Ready()
    {
        FindRequiredNodes();
        
        if (ValidateNodes())
        {
            SetupInitialState();
            SetupEntities();
            ConnectSignals();
            StartPlayerTurn();
        }
        else
        {
            GD.PrintErr("[Battle] Failed to find required nodes");
        }
    }
    
    private void FindRequiredNodes()
    {
        hexGrid = GetNode<HexGrid>("../HexGrid");
        hexControls = hexGrid?.GetNodeOrNull<HexControls>("HexControls");
        menuControls = FindMenuControlsInTree(GetTree().CurrentScene);
        inputManager = FindInputManagerInTree(GetTree().CurrentScene);
    }
    
    private bool ValidateNodes()
    {
        return hexGrid != null && 
               menuControls != null && 
               hexControls != null && 
               inputManager != null;
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
            hexGrid.SetTileByCoords(playerPosition, CellLayer.Entity, new Vector2I(0, 0));
            hexGrid.SetOccupied(playerPosition);
            
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
            hexControls.MovementCancelled += OnMovementCancelled;
    }
    
    #endregion
    
    #region Turn Management
    
    private void StartPlayerTurn()
    {
        if (menuControls != null)
        {
            menuControls.SetActive(true);
        }
        
        isInSubmenu = false;
        currentSubmenuType = "";
        GD.Print("[Battle] Player turn started");
    }
    
    private void EndPlayerTurn()
    {
        if (menuControls != null)
        {
            menuControls.SetActive(false);
        }
        
        StartEnemyTurn();
    }
    
    private void StartEnemyTurn()
    {
        GD.Print("[Battle] Enemy turn started");
        
        GetTree().CreateTimer(1.0).Connect("timeout", 
            new Callable(this, nameof(ExecuteEnemyAction)));
    }
    
    private void ExecuteEnemyAction()
    {
        GD.Print("[Battle] Enemy attacks!");
        StartPlayerTurn();
    }
    
    #endregion
    
    #region Input Handling
    
    private void OnButtonPressed(int index, BaseButton button)
    {
        if (isInSubmenu)
        {
            HandleSubmenuSelection(index, button);
        }
        else
        {
            HandleMainMenuSelection(button);
        }
    }
    
    private void HandleMainMenuSelection(BaseButton button)
    {
        string buttonText = button.Name.ToString().ToLower();
        
        if (buttonText.Contains("move"))
        {
            StartMovementMode();
        }
        else if (buttonText.Contains("skill"))
        {
            ShowSubmenu("skill", skillOptions);
        }
        else if (buttonText.Contains("item"))
        {
            ShowSubmenu("item", itemOptions);
        }
        else if (buttonText.Contains("talk"))
        {
            ShowSubmenu("talk", talkOptions);
        }
        else
        {
            GD.Print($"[Battle] Button '{button.Name}' pressed - no action defined");
            StartPlayerTurn();
        }
    }
    
    private void HandleSubmenuSelection(int index, BaseButton button)
    {
        string selectedOption = GetSubmenuOption(index);
        
        GD.Print($"[Battle] {currentSubmenuType.ToUpper()} selected: {selectedOption} (Index: {index})");
        
        switch (currentSubmenuType)
        {
            case "skill":
                ExecuteSkill(selectedOption);
                break;
            case "item":
                UseItem(selectedOption);
                break;
            case "talk":
                ExecuteTalkAction(selectedOption);
                break;
        }
        
        CloseSubmenu();
    }
    
    private string GetSubmenuOption(int index)
    {
        string[] currentOptions = currentSubmenuType switch
        {
            "skill" => skillOptions,
            "item" => itemOptions,
            "talk" => talkOptions,
            _ => new string[0]
        };
        
        return index < currentOptions.Length ? currentOptions[index] : "Unknown";
    }
    
    #endregion
    
    #region Submenu Management
    
    private void ShowSubmenu(string submenuType, string[] options)
    {
        GD.Print($"[Battle] Opening {submenuType} submenu");
        
        isInSubmenu = true;
        currentSubmenuType = submenuType;
        
        if (menuControls != null)
        {
            menuControls.SetActive(false);
        }
        
        if (inputManager != null)
        {
            inputManager.SetMenuButtonArray(options);
            ConnectToSubmenu();
        }
        else
        {
            GD.PrintErr("[Battle] Cannot show submenu - CentralInputManager not found");
            StartPlayerTurn();
        }
    }
    
    private void ConnectToSubmenu()
    {
        var dynamicMenu = FindDynamicMenu();
        if (dynamicMenu != null)
        {
            if (!dynamicMenu.IsConnected(MenuControls.SignalName.ButtonActivated, 
                new Callable(this, nameof(OnButtonPressed))))
            {
                dynamicMenu.ButtonActivated += OnButtonPressed;
            }
        }
    }
    
    private void CloseSubmenu()
    {
        isInSubmenu = false;
        currentSubmenuType = "";
        
        if (inputManager != null)
        {
            inputManager.ClearDynamicMenu();
        }
        
        StartPlayerTurn();
    }
    
    private MenuControls FindDynamicMenu()
    {
        var controlNode = GetTree().CurrentScene.GetNodeOrNull<Control>("Control");
        if (controlNode != null)
        {
            return FindMenuControlsInTree(controlNode);
        }
        return null;
    }
    
    #endregion
    
    #region Movement System
    
    private void StartMovementMode()
    {
        GD.Print("[Battle] Starting movement mode");
        
        if (menuControls != null)
        {
            menuControls.SetActive(false);
        }
        
        movementModeActive = true;
        validMoves = GetAdjacentWalkableTiles(playerPosition);
        UpdateMovementVisuals();
        
        if (hexControls != null)
        {
            hexControls.SetValidMoves(validMoves);
            hexControls.EnterMovementMode();
        }
    }
    
    private void ExitMovementMode()
    {
        movementModeActive = false;
        ClearMovementVisuals();
        
        if (hexControls != null)
        {
            hexControls.ExitMovementMode(playerPosition);
        }
        
        GD.Print("[Battle] Exited movement mode");
        StartPlayerTurn();
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
            GD.Print($"[Battle] Invalid move to {cell}");
        }
    }
    
    private void ExecuteMove(Vector2I targetPosition)
    {
        if (!validMoves.Contains(targetPosition))
        {
            GD.Print($"[Battle] Cannot move to {targetPosition} - invalid");
            return;
        }
        
        GD.Print($"[Battle] Moving player from {playerPosition} to {targetPosition}");
        
        hexGrid.ClearTile(playerPosition, CellLayer.Entity);
        hexGrid.SetOccupied(playerPosition, false);
        
        playerPosition = targetPosition;
        hexGrid.SetTileByCoords(playerPosition, CellLayer.Entity, new Vector2I(0, 0));
        hexGrid.SetOccupied(playerPosition);
        
        ExitMovementMode();
    }
    
    private HashSet<Vector2I> GetAdjacentWalkableTiles(Vector2I center)
    {
        var adjacent = new HashSet<Vector2I>();
        var neighbors = hexGrid.GetNeighbors(center);
        
        foreach (var neighbor in neighbors)
        {
            adjacent.Add(neighbor);
        }
        
        return adjacent;
    }
    
    private void UpdateMovementVisuals()
    {
        var markerLayer = hexGrid.GetLayer(CellLayer.Marker);
        markerLayer?.Clear();
        
        foreach (var move in validMoves)
        {
            hexGrid.SetTileByCoords(move, CellLayer.Marker, new Vector2I(1, 0));
        }
    }
    
    private void ClearMovementVisuals()
    {
        var markerLayer = hexGrid.GetLayer(CellLayer.Marker);
        markerLayer?.Clear();
        
        var cursorLayer = hexGrid.GetLayer(CellLayer.Cursor);
        cursorLayer?.Clear();
    }
    
    #endregion
    
    #region Battle Actions
    
    private void ExecuteSkill(string skillName)
    {
        switch (skillName)
        {
            case "Fireball":
                GD.Print("[Battle] Casting Fireball!");
                break;
            case "Ice Thorn":
                GD.Print("[Battle] Casting Ice Thorn!");
                break;
            case "Shroud":
                GD.Print("[Battle] Casting Shroud!");
                break;
            default:
                GD.Print($"[Battle] Unknown skill: {skillName}");
                break;
        }
    }
    
    private void UseItem(string itemName)
    {
        switch (itemName)
        {
            case "Potion":
                GD.Print("[Battle] Using Potion!");
                break;
            case "Ether":
                GD.Print("[Battle] Using Ether!");
                break;
            case "Phoenix Down":
                GD.Print("[Battle] Using Phoenix Down!");
                break;
            default:
                GD.Print($"[Battle] Unknown item: {itemName}");
                break;
        }
    }
    
    private void ExecuteTalkAction(string action)
    {
        switch (action)
        {
            case "Negotiate":
                GD.Print("[Battle] Attempting to negotiate!");
                break;
            case "Intimidate":
                GD.Print("[Battle] Attempting to intimidate!");
                break;
            case "Flee":
                GD.Print("[Battle] Attempting to flee!");
                break;
            default:
                GD.Print($"[Battle] Unknown talk action: {action}");
                break;
        }
    }
    
    #endregion
    
    #region Helper Methods
    
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
    
    #endregion
}