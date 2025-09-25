// HexGrid.cs - Core abstracted hex grid
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
    [Signal] public delegate void CellSelectedEventHandler(Vector2I cell);

    [Export] private TileMapLayer terrainLayer;
    [Export] private TileMapLayer worldMarkerLayer;
    [Export] private TileMapLayer obstacleLayer;
    [Export] private TileMapLayer entityLayer;
    [Export] private TileMapLayer markerLayer;
    [Export] private TileMapLayer cursorLayer;

    [Export] private bool enableMouseDebug = false;

    private Vector2I selectedCell = new(-999, -999);
    private HashSet<Vector2I> occupiedCells = new();

    public Vector2I Selected => selectedCell;
    public bool HasSelection => selectedCell != new Vector2I(-999, -999);

    public override void _Ready()
    {
        terrainLayer ??= GetNode<TileMapLayer>("Terrain");
        worldMarkerLayer ??= GetNode<TileMapLayer>("WorldMarker");
        obstacleLayer ??= GetNode<TileMapLayer>("Obstacle");
        entityLayer ??= GetNode<TileMapLayer>("Entity");
        markerLayer ??= GetNode<TileMapLayer>("Marker");
        cursorLayer ??= GetNode<TileMapLayer>("Cursor");

        // TestAllTileIDs();
    }

    public override void _Process(double delta)
    {
        if (enableMouseDebug)
        {
            var mouseCell = GetMouseCell();
            if (IsValid(mouseCell))
                GD.Print($"[Debug] Mouse at hex: {mouseCell}");
        }
    }

    #region Core Interface

    public void Select(Vector2I cell)
    {
        if (!IsValid(cell)) return;
        selectedCell = cell;
        cursorLayer?.Clear();
        cursorLayer?.SetCell(cell, 0, Vector2I.Zero, 0);
        EmitSignal(SignalName.CellSelected, cell);
    }

    public void Clear()
    {
        selectedCell = new Vector2I(-999, -999);
        cursorLayer?.Clear();
    }

    public Vector2 GetPosition(Vector2I cell) => terrainLayer?.MapToLocal(cell) ?? Vector2.Zero;
    public Vector2I GetCell(Vector2 worldPos) => terrainLayer?.LocalToMap(ToLocal(worldPos)) ?? Vector2I.Zero;
    public Vector2I GetMouseCell() => GetCell(GetGlobalMousePosition());

    public bool IsValid(Vector2I cell) => terrainLayer?.GetCellTileData(cell) != null;
    public bool IsWalkable(Vector2I cell) => IsValid(cell) && !IsBlocked(cell) && !IsOccupied(cell);
    public bool IsBlocked(Vector2I cell) => obstacleLayer?.GetCellTileData(cell) != null;
    public bool IsOccupied(Vector2I cell) => occupiedCells.Contains(cell);

    public void SetOccupied(Vector2I cell, bool occupied = true)
    {
        if (occupied) occupiedCells.Add(cell);
        else occupiedCells.Remove(cell);
    }

    public void ClearTile(Vector2I cell, CellLayer layer)
    {
        GetLayer(layer)?.EraseCell(cell);
    }

    public void Highlight(List<Vector2I> cells, int tileId)
    {
        cursorLayer?.Clear();
        foreach (var cell in cells)
        {
            if (IsValid(cell))
                cursorLayer?.SetCell(cell, 0, Vector2I.Zero, tileId);
        }
    }
        public void SetTile(Vector2I cell, CellLayer layer, int tileId)
    {
        GetLayer(layer)?.SetCell(cell, 0, Vector2I.Zero, tileId);
    }

    public void SetTileByCoords(Vector2I cell, CellLayer layer, Vector2I tileCoords)
    {
        GetLayer(layer)?.SetCell(cell, 0, tileCoords, 0);
    }

    #endregion

    #region Pathfinding

    public List<Vector2I> GetNeighbors(Vector2I cell)
    {
        var neighbors = new List<Vector2I>();
        Vector2I[] directions = { new(1, 0), new(0, 1), new(-1, 1), new(-1, 0), new(0, -1), new(1, -1) };

        foreach (var dir in directions)
        {
            var neighbor = cell + dir;
            if (IsWalkable(neighbor)) neighbors.Add(neighbor);
        }
        return neighbors;
    }

    public List<Vector2I> GetReachable(Vector2I origin, int range)
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
                foreach (var neighbor in GetNeighbors(current))
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

    public int GetDistance(Vector2I a, Vector2I b)
    {
        var cubeA = OffsetToCube(a);
        var cubeB = OffsetToCube(b);
        return (Mathf.Abs(cubeA.X - cubeB.X) + Mathf.Abs(cubeA.Y - cubeB.Y) + Mathf.Abs(cubeA.Z - cubeB.Z)) / 2;
    }

    #endregion

    #region Internal

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

    private Vector3I OffsetToCube(Vector2I offset)
    {
        int col = offset.X, row = offset.Y;
        int x = col - (row - (row & 1)) / 2;
        int z = row;
        return new Vector3I(x, -x - z, z);
    }

    #endregion
    

   private void TestAllTileIDs()
{
    var testPos = new Vector2I(0, 0);
    
    GD.Print("=== Testing TileSet Sources ===");
    
    // Check what's actually in the TileSet
    if (markerLayer?.TileSet != null)
    {
        var tileSet = markerLayer.TileSet;
        GD.Print($"TileSet has {tileSet.GetSourceCount()} sources");
        
        for (int sourceId = 0; sourceId < tileSet.GetSourceCount(); sourceId++)
        {
            var source = tileSet.GetSource(sourceId);
            GD.Print($"Source {sourceId}: {source.GetType().Name}");
            
            if (source is TileSetAtlasSource atlasSource)
            {
                GD.Print($"Atlas source has {atlasSource.GetTilesCount()} tiles");
                
                // Try to get tile coordinates
                for (int i = 0; i < atlasSource.GetTilesCount(); i++)
                {
                    var tileCoords = atlasSource.GetTileId(i);
                    GD.Print($"Tile {i}: coords {tileCoords}");
                    
                    // Test setting this tile
                    markerLayer.SetCell(testPos, sourceId, tileCoords, 0);
                    GD.Print($"Set tile at source {sourceId}, coords {tileCoords}");
                }
            }
        }
    }
}
}
