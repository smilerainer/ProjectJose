// HexGrid.cs - Node2D that manages multiple TileMapLayer children
using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class HexGrid : Node2D
{
    // Layer assignments
    [ExportGroup("Tilemap Layers")]
    [Export] private TileMapLayer groundLayer; // Base terrain tiles
    [Export] private TileMapLayer obstacleLayer; // Walls, rocks, etc
    [Export] private TileMapLayer entityLayer; // For entity position markers (optional)
    [Export] private TileMapLayer selectionLayer; // Yellow cursor selection
    [Export] private TileMapLayer movementLayer; // Blue movement highlights
    [Export] private TileMapLayer targetLayer; // Red/Green skill targeting
    
    [ExportGroup("Tile IDs")]
    [Export] private int cursorTileId = 0; // Yellow cursor tile
    [Export] private int moveTileId = 0; // Blue movement tile
    [Export] private int attackTileId = 0; // Red attack tile
    [Export] private int supportTileId = 0; // Green support tile
    
    [ExportGroup("Grid Settings")]
    [Export] private int gridWidth = 15;
    [Export] private int gridHeight = 10;
    [Export] private bool useTileMapBounds = true; // Use actual tiles for bounds instead of fixed size
    
    private Dictionary<Vector2I, bool> occupiedCells = new();
    private List<Vector2I> moveHighlights = new();
    private List<Vector2I> targetHighlights = new();
    private Vector2I cursorPosition = Vector2I.Zero;
    private bool cursorVisible = false;
    
    public enum HighlightType
    {
        Cursor,
        Movement,
        Attack,
        Support
    }
    
    public override void _Ready()
    {
        ValidateLayers();
        GD.Print($"HexGrid ready - Managing {GetChildCount()} layers");
    }
    
    private void ValidateLayers()
    {
        // Auto-assign layers if not set
        if (groundLayer == null)
            groundLayer = GetNodeOrNull<TileMapLayer>("GroundLayer");
        if (obstacleLayer == null)
            obstacleLayer = GetNodeOrNull<TileMapLayer>("ObstacleLayer");
        if (entityLayer == null)
            entityLayer = GetNodeOrNull<TileMapLayer>("EntityLayer");
        if (selectionLayer == null)
            selectionLayer = GetNodeOrNull<TileMapLayer>("SelectionLayer");
        if (movementLayer == null)
            movementLayer = GetNodeOrNull<TileMapLayer>("MovementLayer");
        if (targetLayer == null)
            targetLayer = GetNodeOrNull<TileMapLayer>("TargetLayer");
            
        // Warn about missing critical layers
        if (groundLayer == null)
            GD.PrintErr("Warning: No ground layer assigned!");
        if (selectionLayer == null)
            GD.PrintErr("Warning: No selection layer assigned!");
    }
    
    public Vector2 GetHexWorldPosition(Vector2I coord)
    {
        // Use the ground layer for coordinate conversion
        if (groundLayer != null)
            return groundLayer.MapToLocal(coord);
            
        // Fallback to first available layer
        var firstLayer = GetChild<TileMapLayer>(0);
        return firstLayer?.MapToLocal(coord) ?? Vector2.Zero;
    }
    
    public Vector2I GetHexFromWorld(Vector2 worldPos)
    {
        // Use the ground layer for coordinate conversion
        if (groundLayer != null)
            return groundLayer.LocalToMap(worldPos);
            
        // Fallback to first available layer
        var firstLayer = GetChild<TileMapLayer>(0);
        return firstLayer?.LocalToMap(worldPos) ?? Vector2I.Zero;
    }
    
    public List<Vector2I> GetNeighbors(Vector2I hex)
    {
        var neighbors = new List<Vector2I>();
        
        // For flat-top hexagons
        Vector2I[] directions = {
            new(1, 0), new(0, 1), new(-1, 1),
            new(-1, 0), new(0, -1), new(1, -1)
        };
        
        foreach (var dir in directions)
        {
            var neighbor = hex + dir;
            if (IsValidHex(neighbor) && IsWalkable(neighbor))
                neighbors.Add(neighbor);
        }
        return neighbors;
    }
    
    public bool IsValidHex(Vector2I coord)
    {
        if (useTileMapBounds && groundLayer != null)
        {
            // Check if there's a ground tile at this position
            var tileData = groundLayer.GetCellTileData(coord);
            return tileData != null;
        }
        
        // Fallback to fixed bounds
        return coord.X >= 0 && coord.X < gridWidth && 
               coord.Y >= 0 && coord.Y < gridHeight;
    }
    
    public bool IsWalkable(Vector2I coord)
    {
        // Check if not blocked by obstacle
        if (obstacleLayer != null)
        {
            var obstacleData = obstacleLayer.GetCellTileData(coord);
            if (obstacleData != null)
                return false;
        }
        
        // Check if not occupied by entity
        return !IsOccupied(coord);
    }
    
    public int GetDistance(Vector2I a, Vector2I b)
    {
        // Convert offset to cube coordinates for accurate distance
        var cubeA = OffsetToCube(a);
        var cubeB = OffsetToCube(b);
        
        return (Mathf.Abs(cubeA.X - cubeB.X) + 
                Mathf.Abs(cubeA.Y - cubeB.Y) + 
                Mathf.Abs(cubeA.Z - cubeB.Z)) / 2;
    }
    
    private Vector3I OffsetToCube(Vector2I offset)
    {
        // Odd-r offset layout (odd rows are offset right)
        int col = offset.X;
        int row = offset.Y;
        int cubeX = col - (row - (row & 1)) / 2;
        int cubeZ = row;
        int cubeY = -cubeX - cubeZ;
        return new Vector3I(cubeX, cubeY, cubeZ);
    }
    
    // Entity occupation management
    public void SetOccupied(Vector2I coord, bool occupied)
    {
        occupiedCells[coord] = occupied;
        
        // Optionally mark on entity layer
        if (entityLayer != null && occupied)
        {
            // You can set a marker tile here if desired
            // entityLayer.SetCell(coord, 0, Vector2I.Zero, entityMarkerTileId);
        }
        else if (entityLayer != null && !occupied)
        {
            entityLayer.EraseCell(coord);
        }
    }
    
    public bool IsOccupied(Vector2I coord)
    {
        return occupiedCells.GetValueOrDefault(coord, false);
    }
    
    // Cursor management
    public void ShowCursor(Vector2I coord)
    {
        if (selectionLayer == null) return;
        
        HideCursor();
        cursorPosition = coord;
        cursorVisible = true;
        selectionLayer.SetCell(coord, 0, Vector2I.Zero, cursorTileId);
    }
    
    public void HideCursor()
    {
        if (selectionLayer == null || !cursorVisible) return;
        
        selectionLayer.EraseCell(cursorPosition);
        cursorVisible = false;
    }
    
    public void MoveCursor(Vector2I direction)
    {
        var newPos = cursorPosition + direction;
        if (IsValidHex(newPos))
        {
            ShowCursor(newPos);
        }
    }
    
    public Vector2I GetCursorPosition() => cursorPosition;
    
    // Movement highlighting
    public void ShowMovementRange(Vector2I origin, int range)
    {
        ClearMovementHighlights();
        if (movementLayer == null) return;
        
        // Find all reachable hexes within range
        var reachable = GetReachableHexes(origin, range);
        
        foreach (var hex in reachable)
        {
            if (hex != origin)
            {
                movementLayer.SetCell(hex, 0, Vector2I.Zero, moveTileId);
                moveHighlights.Add(hex);
            }
        }
    }
    
    public void ClearMovementHighlights()
    {
        if (movementLayer == null) return;
        
        foreach (var hex in moveHighlights)
        {
            movementLayer.EraseCell(hex);
        }
        moveHighlights.Clear();
    }
    
    // Target highlighting (for skills)
    public void ShowTargetArea(Vector2I center, int range, HighlightType type)
    {
        ClearTargetHighlights();
        if (targetLayer == null) return;
        
        int tileId = type == HighlightType.Attack ? attackTileId : supportTileId;
        
        // Get all hexes within range
        var targets = GetHexesInRange(center, range);
        
        foreach (var hex in targets)
        {
            targetLayer.SetCell(hex, 0, Vector2I.Zero, tileId);
            targetHighlights.Add(hex);
        }
    }
    
    public void ShowTargetLine(Vector2I origin, Vector2I direction, int range, HighlightType type)
    {
        ClearTargetHighlights();
        if (targetLayer == null) return;
        
        int tileId = type == HighlightType.Attack ? attackTileId : supportTileId;
        
        // Show line of hexes in direction
        for (int i = 1; i <= range; i++)
        {
            var hex = origin + (direction * i);
            if (IsValidHex(hex))
            {
                targetLayer.SetCell(hex, 0, Vector2I.Zero, tileId);
                targetHighlights.Add(hex);
            }
        }
    }
    
    public void ClearTargetHighlights()
    {
        if (targetLayer == null) return;
        
        foreach (var hex in targetHighlights)
        {
            targetLayer.EraseCell(hex);
        }
        targetHighlights.Clear();
    }
    
    public void ClearAllHighlights()
    {
        HideCursor();
        ClearMovementHighlights();
        ClearTargetHighlights();
    }
    
    // Utility functions for getting hex sets
    public List<Vector2I> GetHexesInRange(Vector2I center, int range)
    {
        var hexes = new List<Vector2I>();
        
        for (int dx = -range; dx <= range; dx++)
        {
            for (int dy = Mathf.Max(-range, -dx - range); dy <= Mathf.Min(range, -dx + range); dy++)
            {
                var hex = CubeToOffset(new Vector3I(dx, dy, -dx - dy) + OffsetToCube(center));
                if (IsValidHex(hex))
                    hexes.Add(hex);
            }
        }
        
        return hexes;
    }
    
    public List<Vector2I> GetReachableHexes(Vector2I origin, int moveRange)
    {
        var reachable = new List<Vector2I>();
        var visited = new HashSet<Vector2I>();
        var frontier = new Queue<(Vector2I pos, int dist)>();
        
        frontier.Enqueue((origin, 0));
        visited.Add(origin);
        
        while (frontier.Count > 0)
        {
            var (current, dist) = frontier.Dequeue();
            reachable.Add(current);
            
            if (dist < moveRange)
            {
                foreach (var neighbor in GetNeighbors(current))
                {
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        frontier.Enqueue((neighbor, dist + 1));
                    }
                }
            }
        }
        
        return reachable;
    }
    
    private Vector2I CubeToOffset(Vector3I cube)
    {
        int col = cube.X + (cube.Z - (cube.Z & 1)) / 2;
        int row = cube.Z;
        return new Vector2I(col, row);
    }
}

