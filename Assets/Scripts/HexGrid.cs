// HexGrid.cs - Enhanced core hex grid with yellow AOE cursors
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
    private HashSet<Vector2I> currentAoeCells = new(); // Track current AOE cells
    
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
    
    public void HighlightCells(List<Vector2I> cells, int tileId)
    {
        cursorLayer?.Clear();
        foreach (var cell in cells)
        {
            if (IsValidCell(cell))
                cursorLayer?.SetCell(cell, 0, Vector2I.Zero, tileId);
        }
    }
    
    private void UpdateCursorVisual()
    {
        cursorLayer?.Clear();
        if (HasSelection)
            cursorLayer?.SetCell(selectedCell, 0, Vector2I.Zero, 0);
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
        var metaKey = $"{cell.X},{cell.Y}:{key}";
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
    
    #region Pathfinding and Navigation
    
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
        // Fixed for VERTICAL offset hex coordinates
        Vector2I[] evenColDirections = { 
            new(0, -1),  // N
            new(1, -1),  // NE
            new(1, 0),   // SE  
            new(0, 1),   // S
            new(-1, 0),  // SW
            new(-1, -1)  // NW
        };
        
        Vector2I[] oddColDirections = { 
            new(0, -1),  // N
            new(1, 0),   // NE
            new(1, 1),   // SE
            new(0, 1),   // S
            new(-1, 1),  // SW
            new(-1, 0)   // NW
        };

        bool isEvenCol = cell.X % 2 == 0;
        return isEvenCol ? evenColDirections : oddColDirections;
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
        var markerLayer = GetLayer(CellLayer.Marker);
        markerLayer?.Clear();
        
        foreach (var cell in rangeCells)
        {
            if (IsValidCell(cell))
            {
                SetTileWithCoords(cell, CellLayer.Marker, new Vector2I(1, 0)); // Blue range marker
                GD.Print($"[HexGrid] Range highlight at {cell}");
            }
        }
    }
    
    public void ShowAoePreview(Vector2I targetCell, List<Vector2I> aoePattern)
    {
        GD.Print($"[HexGrid] ShowAoePreview called - Target: {targetCell}, AOE cells: {aoePattern.Count}");
        
        // Clear existing AOE previews (only yellow cursors)
        ClearAoePreview();
        
        // Track new AOE cells
        currentAoeCells.Clear();
        
        foreach (var offset in aoePattern)
        {
            var aoeCell = targetCell + offset;
            if (IsValidCell(aoeCell))
            {
                // Use YELLOW cursor (atlas coords 0,0) for AOE preview
                // Note: Green cursors will be drawn after this to override when needed
                cursorLayer?.SetCell(aoeCell, 0, new Vector2I(0, 0), 0);
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
        var markerLayer = GetLayer(CellLayer.Marker);
        markerLayer?.Clear();
        GD.Print("[HexGrid] Cleared range highlight");
    }
    
    public void ClearAoePreview()
    {
        if (cursorLayer == null) 
        {
            GD.Print("[HexGrid] ClearAoePreview - cursorLayer is null");
            return;
        }
        
        // Only clear AOE cells (yellow cursors), leave other cursor types
        foreach (var aoeCell in currentAoeCells)
        {
            cursorLayer.EraseCell(aoeCell);
            GD.Print($"[HexGrid] Cleared AOE preview at {aoeCell}");
        }
        
        currentAoeCells.Clear();
        GD.Print("[HexGrid] Cleared AOE preview - all yellow cursors removed");
    }
    
    public List<Vector2I> TransformPattern(List<Vector2I> pattern, Vector2I origin)
    {
        var transformedCells = new List<Vector2I>();
        
        foreach (var offset in pattern)
        {
            var transformedCell = origin + offset;
            transformedCells.Add(transformedCell);
            GD.Print($"[HexGrid] Transformed pattern cell: {offset} + {origin} = {transformedCell}");
        }
        
        return transformedCells;
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