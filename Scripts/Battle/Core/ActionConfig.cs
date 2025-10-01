
using System.Collections.Generic;

namespace CustomJsonSystem{
public class TargetPattern
{
    public string Type { get; set; } = "coordinate"; // "coordinate" or "radius"
    public int X { get; set; } = 0;
    public int Y { get; set; } = 0;
    public PatternCell Center { get; set; } = new();
    public int Radius { get; set; } = 0;
}

public class ActionConfig
{
    // Basic properties
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Cost { get; set; } = 0;
    public int Damage { get; set; } = 0;
    public int HealAmount { get; set; } = 0;
    public string StatusEffect { get; set; } = "";
    public int StatusDuration { get; set; } = 0;
    public int Range { get; set; } = 1;
    public string TargetType { get; set; } = "Single";
    public string Dialogue { get; set; } = "";
    public int FriendshipChange { get; set; } = 0;
    public int ReputationChange { get; set; } = 0;
    public int MoneyChange { get; set; } = 0;
    public int UsesRemaining { get; set; } = -1;

    // Pattern properties
    public List<PatternCell> RangePattern { get; set; } = new();
    public List<PatternCell> AoePattern { get; set; } = new();

    // Radius-based properties
    public bool UseRadiusRange { get; set; } = false;
    public int AoeRadius { get; set; } = 0;

    // AOE type and properties
    public string AoeType { get; set; } = ""; // "" (pattern), "radius", "line"
    public int AoeWidth { get; set; } = 1; // Width for line AOE
    public int AoeOvershoot { get; set; } = 0; // How far the line extends past target

    // Targeting system
    public bool AllTilesValid { get; set; } = false;
    public List<TargetPattern> Whitelist { get; set; } = new();
    public List<TargetPattern> Blacklist { get; set; } = new();

    // Target filtering properties
    public bool ExcludeSelf { get; set; } = false;
    public bool ExcludeOccupied { get; set; } = false;
    public bool TargetEmptyCellsOnly { get; set; } = false;
    public bool TargetSelfOnly { get; set; } = false;
    public bool ExcludeOrigin { get; set; } = false; // Exclude origin from AOE
    public List<string> ExcludeTypes { get; set; } = new();

    // Advanced targeting properties
    public bool InverseAOE { get; set; } = false;
    public bool RequiresLineOfSight { get; set; } = false;
    public bool IgnoreObstacles { get; set; } = false;

    // Debug properties
    public bool DebugMode { get; set; } = false;
    public float Speed { get; set; } = 1.0f;

    public Dictionary<string, object> CustomProperties { get; set; } = new();
}
}