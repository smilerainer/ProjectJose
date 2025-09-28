// HexGrid.cs - Standalone hex grid with cursor sprite definitions
using Godot;
using System.Collections.Generic;

public enum CellLayer
{
    Terrain = 0,
    WorldMarker = 10,
    Obstacle = 20,
    Entity = 30,
    Marker = 40,
    Cursor = 50
}

    public static class CursorSprites
    {
        // Updated to match your actual tileset layout
        public static readonly Vector2I GREEN = new Vector2I(2, 0);     // Valid target cursor
        public static readonly Vector2I RED = new Vector2I(3, 0);       // Invalid/adjacent cursor  
        public static readonly Vector2I YELLOW = new Vector2I(1, 0);    // Use blue for AOE preview (since no yellow)
        public static readonly Vector2I BLUE = new Vector2I(1, 0);      // Range highlight cursor
        
        // Alternative tile IDs for layer priority
        public const int ALT_NORMAL = 0;
        public const int ALT_PRIORITY = 1;
    }

public partial class HexGrid : Node2D
{
    #region Signals
    
    [Signal] public delegate void CellSelectedEventHandler(Vector2I cell);
    [Signal] public delegate void CellHoveredEventHandler(Vector2I cell);
    
    #endregion
    
    #region Layer References
    
    [Export] private TileMapLayer terrainLayer;
    [Export] private TileMapLayer worldMarkerLayer;
    [Export] private TileMapLayer obstacleLayer;
    [Export] private TileMapLayer entityLayer;
    [Export] private TileMapLayer markerLayer;
    [Export] private TileMapLayer cursorLayer;
    
    #endregion
    
    #region State
    
    private Vector2I selectedCell = new(-999, -999);
    private Vector2I hoveredCell = new(-999, -999);
    private HashSet<Vector2I> occupiedCells = new();
    private Dictionary<Vector2I, string> cellMetadata = new();
    private HashSet<Vector2I> currentAoeCells = new();
    
    [Export] private bool enableMouseDebug = false;
    [Export] private bool enableHoverTracking = true;
    
    #endregion
    
    #region Properties
    
    public Vector2I SelectedCell => selectedCell;
    public Vector2I HoveredCell => hoveredCell;
    public bool HasSelection => selectedCell != new Vector2I(-999, -999);
    
    #endregion
    
    #region Initialization
    
    public override void _Ready()
    {
        InitializeLayers();
        
        GD.Print("[HexGrid] Cursor sprites configured:");
        GD.Print($"  GREEN (valid): {CursorSprites.GREEN}");
        GD.Print($"  RED (invalid): {CursorSprites.RED}");
        GD.Print($"  YELLOW (AOE): {CursorSprites.YELLOW}");
        GD.Print($"  BLUE (range): {CursorSprites.BLUE}");
    }
    
    private void InitializeLayers()
    {
        terrainLayer ??= GetNode<TileMapLayer>("Terrain");
        worldMarkerLayer ??= GetNode<TileMapLayer>("WorldMarker");
        obstacleLayer ??= GetNode<TileMapLayer>("Obstacle");
        entityLayer ??= GetNode<TileMapLayer>("Entity");
        markerLayer ??= GetNode<TileMapLayer>("Marker");
        cursorLayer ??= GetNode<TileMapLayer>("Cursor");
    }
    
    public override void _Process(double delta)
    {
        if (enableMouseDebug)
        {
            var mouseCell = GetMouseCell();
            if (IsValidCell(mouseCell))
                GD.Print($"[Debug] Mouse at hex: {mouseCell}");
        }
        
        if (enableHoverTracking)
        {
            UpdateHoverTracking();
        }
    }
    
    private void UpdateHoverTracking()
    {
        var currentHover = GetMouseCell();
        if (currentHover != hoveredCell && IsValidCell(currentHover))
        {
            hoveredCell = currentHover;
            EmitSignal(SignalName.CellHovered, hoveredCell);
        }
    }
    
    #endregion
    
    #region Cell Selection Interface
    
    public void SelectCell(Vector2I cell)
    {
        if (!IsValidCell(cell)) return;
        
        selectedCell = cell;
        UpdateCursorVisual();
        EmitSignal(SignalName.CellSelected, cell);
    }
    
    public void ClearSelection()
    {
        selectedCell = new Vector2I(-999, -999);
        cursorLayer?.Clear();
    }
    
    private void UpdateCursorVisual()
    {
        cursorLayer?.Clear();
        if (HasSelection)
            cursorLayer?.SetCell(selectedCell, 0, CursorSprites.GREEN, CursorSprites.ALT_NORMAL);
    }
    
    #endregion
    
    #region Coordinate Conversion
    
    public Vector2 CellToWorld(Vector2I cell) => terrainLayer?.MapToLocal(cell) ?? Vector2.Zero;
    public Vector2I WorldToCell(Vector2 worldPos) => terrainLayer?.LocalToMap(ToLocal(worldPos)) ?? Vector2I.Zero;
    public Vector2I GetMouseCell() => WorldToCell(GetGlobalMousePosition());
    
    #endregion
    
    #region Cell State Queries
    
    public bool IsValidCell(Vector2I cell) => terrainLayer?.GetCellTileData(cell) != null;
    public bool IsWalkableCell(Vector2I cell) => IsValidCell(cell) && !IsBlockedCell(cell) && !IsOccupiedCell(cell);
    public bool IsBlockedCell(Vector2I cell) => obstacleLayer?.GetCellTileData(cell) != null;
    public bool IsOccupiedCell(Vector2I cell) => occupiedCells.Contains(cell);
    
    #endregion
    
    #region Cell State Management
    
    public void SetCellOccupied(Vector2I cell, bool occupied = true)
    {
        if (occupied) 
            occupiedCells.Add(cell);
        else 
            occupiedCells.Remove(cell);
    }
    
    public void SetCellMetadata(Vector2I cell, string key, string value)
    {
        cellMetadata[cell] = value;
    }
    
    public string GetCellMetadata(Vector2I cell, string key, string defaultValue = "")
    {
        return cellMetadata.TryGetValue(cell, out var value) ? value : defaultValue;
    }
    
    #endregion
    
    #region Tile Manipulation
    
    public void SetTile(Vector2I cell, CellLayer layer, int tileId)
    {
        GetLayer(layer)?.SetCell(cell, 0, Vector2I.Zero, tileId);
    }

    public void SetTileWithCoords(Vector2I cell, CellLayer layer, Vector2I tileCoords)
    {
        GetLayer(layer)?.SetCell(cell, 0, tileCoords, 0);
    }
    
    public void ClearTile(Vector2I cell, CellLayer layer)
    {
        GetLayer(layer)?.EraseCell(cell);
    }
    
    public TileMapLayer GetLayer(CellLayer layer) => layer switch
    {
        CellLayer.Terrain => terrainLayer,
        CellLayer.WorldMarker => worldMarkerLayer,
        CellLayer.Obstacle => obstacleLayer,
        CellLayer.Entity => entityLayer,
        CellLayer.Marker => markerLayer,
        CellLayer.Cursor => cursorLayer,
        _ => null
    };
    
    #endregion
    
    #region Navigation
    
    public List<Vector2I> GetNeighborsOf(Vector2I cell)
    {
        var neighbors = new List<Vector2I>();
        var directions = GetDirectionsFor(cell);

        foreach (var dir in directions)
        {
            var neighbor = cell + dir;
            if (IsWalkableCell(neighbor)) 
                neighbors.Add(neighbor);
        }
        
        return neighbors;
    }
    
    public List<Vector2I> GetAllNeighborsOf(Vector2I cell)
    {
        var neighbors = new List<Vector2I>();
        var directions = GetDirectionsFor(cell);

        foreach (var dir in directions)
        {
            neighbors.Add(cell + dir);
        }
        
        return neighbors;
    }

    public List<Vector2I> GetCellsInRange(Vector2I origin, int range)
    {
        var reachable = new List<Vector2I>();
        var visited = new HashSet<Vector2I>();
        var queue = new Queue<(Vector2I pos, int dist)>();

        queue.Enqueue((origin, 0));
        visited.Add(origin);

        while (queue.Count > 0)
        {
            var (current, dist) = queue.Dequeue();
            reachable.Add(current);

            if (dist < range)
            {
                foreach (var neighbor in GetNeighborsOf(current))
                {
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue((neighbor, dist + 1));
                    }
                }
            }
        }
        return reachable;
    }

    public int GetDistanceBetween(Vector2I cellA, Vector2I cellB)
    {
        var cubeA = OffsetToCube(cellA);
        var cubeB = OffsetToCube(cellB);
        return (Mathf.Abs(cubeA.X - cubeB.X) + Mathf.Abs(cubeA.Y - cubeB.Y) + Mathf.Abs(cubeA.Z - cubeB.Z)) / 2;
    }
    
    private Vector2I[] GetDirectionsFor(Vector2I cell)
    {
        Vector2I[] evenColDirections = { 
            new(0, -1), new(1, -1), new(1, 0), new(0, 1), new(-1, 0), new(-1, -1)
        };
        
        Vector2I[] oddColDirections = { 
            new(0, -1), new(1, 0), new(1, 1), new(0, 1), new(-1, 1), new(-1, 0)
        };

        return cell.X % 2 == 0 ? evenColDirections : oddColDirections;
    }

    private Vector3I OffsetToCube(Vector2I offset)
    {
        int col = offset.X, row = offset.Y;
        int x = col - (row - (row & 1)) / 2;
        int z = row;
        return new Vector3I(x, -x - z, z);
    }
    
    #endregion
    
    #region Targeting and Highlighting System
    
    public void ShowRangeHighlight(List<Vector2I> rangeCells)
    {
        markerLayer?.Clear();
        
        foreach (var cell in rangeCells)
        {
            if (IsValidCell(cell))
            {
                SetTileWithCoords(cell, CellLayer.Marker, new Vector2I(1, 0));
                GD.Print($"[HexGrid] Range highlight at {cell}");
            }
        }
    }
    
    public void ShowAoePreview(Vector2I targetCell, List<Vector2I> aoePattern)
    {
        GD.Print($"[HexGrid] ShowAoePreview called - Target: {targetCell}, AOE cells: {aoePattern.Count}");
        
        ClearAoePreview();
        currentAoeCells.Clear();
        
        foreach (var offset in aoePattern)
        {
            var aoeCell = targetCell + offset;
            if (IsValidCell(aoeCell))
            {
                cursorLayer?.SetCell(aoeCell, 0, CursorSprites.YELLOW, CursorSprites.ALT_NORMAL);
                currentAoeCells.Add(aoeCell);
                GD.Print($"[HexGrid] AOE preview (YELLOW) at {aoeCell} (offset {offset})");
            }
            else
            {
                GD.Print($"[HexGrid] Skipping invalid AOE cell: {aoeCell} (offset {offset})");
            }
        }
        
        GD.Print($"[HexGrid] AOE preview complete - {currentAoeCells.Count} yellow cursors placed");
    }
    
    public void ClearRangeHighlight()
    {
        markerLayer?.Clear();
        GD.Print("[HexGrid] Cleared range highlight");
    }
    
    public void ClearAoePreview()
    {
        if (cursorLayer == null) return;
        
        // Only clear yellow cursors, leave green/red alone
        foreach (var aoeCell in currentAoeCells)
        {
            var currentTile = cursorLayer.GetCellAtlasCoords(aoeCell);
            if (currentTile == CursorSprites.YELLOW) // Only clear yellow
            {
                cursorLayer.EraseCell(aoeCell);
            }
        }
        
        currentAoeCells.Clear();
    }
    
    public bool CanTargetSelf(string targetType)
    {
        return targetType.ToLower() switch
        {
            "self" => true,
            "ally" => true,
            "any" => true,
            _ => false
        };
    }
    
    #endregion
}