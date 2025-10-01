// HexGrid.cs - Pure hex math engine and display layer
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

public enum CursorType
{
    Valid,      // Green - valid target
    Invalid,    // Red - invalid target  
    AOE,        // Yellow - area of effect
    Range       // Blue - range highlight
}

public static class CursorSprites
{
    // Cursor atlas coordinates
    public static readonly Vector2I VALID = new Vector2I(2, 0);     // Green cursor
    public static readonly Vector2I INVALID = new Vector2I(3, 0);   // Red cursor  
    public static readonly Vector2I AOE = new Vector2I(4, 0);       // Yellow cursor
    public static readonly Vector2I RANGE = new Vector2I(1, 0);     // Blue cursor
    
    public static Vector2I GetSpriteCoords(CursorType type) => type switch
    {
        CursorType.Valid => VALID,
        CursorType.Invalid => INVALID,
        CursorType.AOE => AOE,
        CursorType.Range => RANGE,
        _ => VALID
    };
}

public partial class HexGrid : Node2D
{
    #region Debug Configuration

    [Export] private bool enableDebugLogs = true;

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            GD.Print(message);
        }
    }

    #endregion

    #region Layer References

    [Export] private TileMapLayer terrainLayer;
    [Export] private TileMapLayer worldMarkerLayer;
    [Export] private TileMapLayer obstacleLayer;
    [Export] private TileMapLayer entityLayer;
    [Export] private TileMapLayer markerLayer;
    [Export] private TileMapLayer cursorLayer;

    #endregion

    #region State Management

    private HashSet<Vector2I> occupiedCells = new();
    private Dictionary<Vector2I, string> cellMetadata = new();
    private HashSet<Vector2I> currentRangeCells = new();
    private HashSet<Vector2I> currentAoeCells = new();
    private Dictionary<Vector2I, CursorType> originalCursorTypes = new();

    #endregion

    #region Initialization

    public override void _Ready()
    {
        InitializeLayers();

        DebugLog("[HexGrid] Pure math engine initialized");
        DebugLog($"  VALID (green): {CursorSprites.VALID}");
        DebugLog($"  INVALID (red): {CursorSprites.INVALID}");
        DebugLog($"  AOE (yellow): {CursorSprites.AOE}");
        DebugLog($"  RANGE (blue): {CursorSprites.RANGE}");
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

    #endregion

    #region Pure Coordinate Math

    public Vector2 CellToWorld(Vector2I cell) => terrainLayer?.MapToLocal(cell) ?? Vector2.Zero;
    public Vector2I WorldToCell(Vector2 worldPos) => terrainLayer?.LocalToMap(ToLocal(worldPos)) ?? Vector2I.Zero;

    public List<Vector2I> GetHexNeighbors(Vector2I cell)
    {
        var directions = GetDirectionsFor(cell);
        var neighbors = new List<Vector2I>();

        foreach (var dir in directions)
        {
            neighbors.Add(cell + dir);
        }

        return neighbors;
    }

    public List<Vector2I> GetWalkableNeighbors(Vector2I cell)
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
                foreach (var neighbor in GetWalkableNeighbors(current))
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

    public int GetHexDistance(Vector2I cellA, Vector2I cellB)
    {
        var cubeA = OffsetToCube(cellA);
        var cubeB = OffsetToCube(cellB);
        return (Mathf.Abs(cubeA.X - cubeB.X) + Mathf.Abs(cubeA.Y - cubeB.Y) + Mathf.Abs(cubeA.Z - cubeB.Z)) / 2;
    }

    public Vector2I AdjustOffsetForHexGrid(Vector2I offset, Vector2I referencePosition)
    {
        // Hex grid coordinate adjustment for odd/even columns
        if (referencePosition.X % 2 != 0 && offset.X != 0)
        {
            return new Vector2I(offset.X, offset.Y + 1);
        }
        return offset;
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

    #region Cell State Queries

    public bool IsValidCell(Vector2I cell) => terrainLayer?.GetCellTileData(cell) != null;
    public bool IsWalkableCell(Vector2I cell) => IsValidCell(cell) && !IsBlockedCell(cell) && !IsOccupiedCell(cell);
    public bool IsBlockedCell(Vector2I cell) => obstacleLayer?.GetCellTileData(cell) != null;
    public bool IsOccupiedCell(Vector2I cell) => occupiedCells.Contains(cell);

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

    #region Display Layer Management

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

    public void SetTile(Vector2I cell, CellLayer layer, int tileId)
    {
        GetLayer(layer)?.SetCell(cell, 0, Vector2I.Zero, tileId);
    }

    public void SetTileWithCoords(Vector2I cell, CellLayer layer, Vector2I tileCoords)
    {
        GetLayer(layer)?.SetCell(cell, 0, tileCoords, 0);
    }

    public void SetCursor(Vector2I cell, CursorType cursorType, CellLayer layer = CellLayer.Cursor)
    {
        var coords = CursorSprites.GetSpriteCoords(cursorType);
        GetLayer(layer)?.SetCell(cell, 0, coords, 0);
    }

    public void ClearTile(Vector2I cell, CellLayer layer)
    {
        GetLayer(layer)?.EraseCell(cell);
    }

    public void ClearLayer(CellLayer layer)
    {
        GetLayer(layer)?.Clear();
    }

    #endregion

    #region Highlighting System (Display Only)

    public void ShowRangeHighlight(List<Vector2I> rangeCells)
    {
        ClearRangeHighlight();

        foreach (var cell in rangeCells)
        {
            if (IsValidCell(cell))
            {
                SetCursor(cell, CursorType.Range, CellLayer.Marker);
                currentRangeCells.Add(cell);
            }
        }

        DebugLog($"[HexGrid] Range highlight shown - {currentRangeCells.Count} cells");
    }

    public void ShowAoePreview(Vector2I targetCell, List<Vector2I> aoePattern)
    {
        ClearAoePreview();

        if (aoePattern == null || aoePattern.Count == 0)
            return;

        if (aoePattern.Count == 1 && aoePattern[0] == Vector2I.Zero)
            return;

        foreach (var offset in aoePattern)
        {
            var adjustedOffset = AdjustOffsetForHexGrid(offset, targetCell);
            var aoeCell = targetCell + adjustedOffset;

            //Dont offset aoe's anymore
            // var aoeCell = targetCell;

            if (IsValidCell(aoeCell))
            {
                // Store what was originally at this cell
                if (currentRangeCells.Contains(aoeCell))
                {
                    originalCursorTypes[aoeCell] = CursorType.Range;
                }

                SetCursor(aoeCell, CursorType.AOE, CellLayer.Marker);
                currentAoeCells.Add(aoeCell);
            }
        }

        DebugLog($"[HexGrid] AOE preview shown - {currentAoeCells.Count} cells");
    }

    public void ClearRangeHighlight()
    {
        foreach (var cell in currentRangeCells)
        {
            ClearTile(cell, CellLayer.Marker);
        }
        currentRangeCells.Clear();
        originalCursorTypes.Clear();

        DebugLog("[HexGrid] Range highlight cleared");
    }

    public void ClearAoePreview()
    {
        foreach (var cell in currentAoeCells)
        {
            // Restore original cursor if it was overridden
            if (originalCursorTypes.TryGetValue(cell, out CursorType originalType))
            {
                SetCursor(cell, originalType, CellLayer.Marker);
                originalCursorTypes.Remove(cell);
            }
            else
            {
                ClearTile(cell, CellLayer.Marker);
            }
        }
        currentAoeCells.Clear();

        DebugLog("[HexGrid] AOE preview cleared");
    }

    public void ClearAllHighlights()
    {
        ClearAoePreview();

        foreach (var cell in currentRangeCells)
        {
            ClearTile(cell, CellLayer.Marker);
        }
        currentRangeCells.Clear();
        originalCursorTypes.Clear();

        ClearLayer(CellLayer.Cursor);

        DebugLog("[HexGrid] All highlights cleared");
    }

    #endregion

    #region Entity Management (Pass-through to battle system)

    // These are called by battle system but HexGrid just handles the display
    public void SelectCell(Vector2I cell)
    {
        // This will be connected to battle system via signal
        DebugLog($"[HexGrid] Cell selected: {cell}");
    }
    
    // Add this method to HexGrid.cs in the Highlighting System region

    public void ShowAoePreviewAbsolute(List<Vector2I> absoluteCells)
    {
        ClearAoePreview();
        
        if (absoluteCells == null || absoluteCells.Count == 0)
            return;
        
        foreach (var cell in absoluteCells)
        {
            if (IsValidCell(cell))
            {
                // Store what was originally at this cell
                if (currentRangeCells.Contains(cell))
                {
                    originalCursorTypes[cell] = CursorType.Range;
                }
                
                SetCursor(cell, CursorType.AOE, CellLayer.Marker);
                currentAoeCells.Add(cell);
            }
        }
        
        DebugLog($"[HexGrid] AOE preview shown (absolute) - {currentAoeCells.Count} cells");
    }
    
    #endregion
}