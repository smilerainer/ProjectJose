// SceneManager.cs - Improved scene sequencing
using Godot;
using System.Collections.Generic;
using System.Text.Json;

public partial class SceneManager : Node
{
    #region Configuration
    
    private const string SEQUENCE_DATA_PATH = "res://data/story_sequence.json";
    private const string VN_SCENE = "res://Assets/Dialogue/Scenes/BaseVN.tscn";
    private const string BATTLE_SCENE = "res://Assets/Dialogue/Scenes/BaseBattle.tscn";
    
    [ExportGroup("Debug")]
    [Export] private bool testMode = false;
    [Export] private int testStartIndex = 0;
    [Export] private bool verboseLogging = false;
    
    #endregion
    
    #region State
    
    private Node dialogicAutoload;
    private Dictionary<string, Variant> sceneParameters = new();
    private Dictionary<string, Variant> battleResults = new();
    private List<SequenceEntry> sequence = new();
    private int sequenceIndex = 0;
    private int pValue = 0;
    private bool isTransitioning = false;
    
    #endregion
    
    #region Initialization
    
    public override void _Ready()
    {
        InitializeDialogic();
        LoadSequence(SEQUENCE_DATA_PATH);
    }
    
    private void InitializeDialogic()
    {
        dialogicAutoload = GetNodeOrNull("/root/Dialogic");
        
        if (dialogicAutoload != null)
        {
            dialogicAutoload.Connect("timeline_ended", new Callable(this, nameof(OnTimelineEnded)));
            dialogicAutoload.Connect("signal_event", new Callable(this, nameof(OnDialogicSignal)));
            Log("[SceneManager] Dialogic connected");
        }
        else
        {
            GD.PrintErr("[SceneManager] Dialogic autoload not found!");
        }
        
        Log("[SceneManager] Initialized");
    }
    
    #endregion
    
    #region Sequence Loading
    
    private void LoadSequence(string jsonPath)
    {
        if (!FileAccess.FileExists(jsonPath))
        {
            GD.PrintErr($"[SceneManager] Sequence file not found: {jsonPath}");
            return;
        }
        
        using var file = FileAccess.Open(jsonPath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PrintErr($"[SceneManager] Cannot open file: {jsonPath}");
            return;
        }
        
        string json = file.GetAsText();
        
        try
        {
            var data = JsonSerializer.Deserialize<SequenceData>(json);
            
            if (data?.sequences == null || data.sequences.Count == 0)
            {
                GD.PrintErr("[SceneManager] Invalid or empty sequence data");
                return;
            }
            
            sequence = data.sequences;
            sequenceIndex = testMode ? testStartIndex : 0;
            
            if (testMode)
            {
                GD.Print($"[SceneManager] TEST MODE: Starting at index {testStartIndex}");
                if (verboseLogging) PrintSequenceList();
            }
            
            GD.Print($"[SceneManager] Loaded {sequence.Count} sequence entries");
            
            LoadNextInSequence();
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"[SceneManager] Failed to parse sequence JSON: {e.Message}");
        }
    }
    
    public void LoadNextInSequence()
    {
        if (isTransitioning)
        {
            Log("[SceneManager] Already transitioning, ignoring duplicate call");
            return;
        }
        
        if (sequenceIndex >= sequence.Count)
        {
            GD.Print("[SceneManager] Sequence complete");
            OnSequenceComplete();
            return;
        }
        
        var entry = sequence[sequenceIndex++];
        
        if (!ValidateSequenceEntry(entry))
        {
            GD.PrintErr($"[SceneManager] Invalid sequence entry: {entry.id}, skipping");
            LoadNextInSequence();
            return;
        }
        
        GD.Print($"[SceneManager] Loading [{sequenceIndex}/{sequence.Count}] {entry.id} ({entry.type})");
        
        sceneParameters.Clear();
        ParseCustomData(entry);
        
        isTransitioning = true;
        
        switch (entry.type.ToLower())
        {
            case "vn":
                LoadVNScene(entry);
                break;
                
            case "battle":
                LoadBattleScene(entry);
                break;
                
            default:
                GD.PrintErr($"[SceneManager] Unknown sequence type: {entry.type}");
                isTransitioning = false;
                LoadNextInSequence();
                break;
        }
    }
    
    private bool ValidateSequenceEntry(SequenceEntry entry)
    {
        if (string.IsNullOrEmpty(entry.id))
        {
            GD.PrintErr("[SceneManager] Sequence entry missing ID");
            return false;
        }
        
        if (string.IsNullOrEmpty(entry.type))
        {
            GD.PrintErr($"[SceneManager] Entry {entry.id} missing type");
            return false;
        }
        
        switch (entry.type.ToLower())
        {
            case "vn":
                if (string.IsNullOrEmpty(entry.timeline))
                {
                    GD.PrintErr($"[SceneManager] VN entry {entry.id} missing timeline");
                    return false;
                }
                break;
                
            case "battle":
                if (string.IsNullOrEmpty(entry.config))
                {
                    GD.PrintErr($"[SceneManager] Battle entry {entry.id} missing config");
                    return false;
                }
                if (string.IsNullOrEmpty(entry.map))
                {
                    GD.PrintErr($"[SceneManager] Battle entry {entry.id} missing map");
                    return false;
                }
                break;
        }
        
        return true;
    }
    
    private void ParseCustomData(SequenceEntry entry)
    {
        if (string.IsNullOrEmpty(entry.data)) return;
        
        try
        {
            var dataDict = JsonSerializer.Deserialize<Dictionary<string, object>>(entry.data);
            var godotDict = new Godot.Collections.Dictionary();
            
            foreach (var kvp in dataDict)
            {
                godotDict[kvp.Key] = kvp.Value?.ToString() ?? "";
            }
            
            sceneParameters["data"] = godotDict;
            Log($"[SceneManager] Parsed custom data: {dataDict.Count} entries");
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"[SceneManager] Failed to parse data for {entry.id}: {e.Message}");
        }
    }
    
    private void LoadVNScene(SequenceEntry entry)
    {
        sceneParameters["timeline"] = entry.timeline;
        
        CallDeferred(nameof(DeferredSceneChange), VN_SCENE);
    }

    private void LoadBattleScene(SequenceEntry entry)
    {
        sceneParameters["battle_config"] = entry.config;
        sceneParameters["map"] = entry.map;  // Pass map path to BattleManager
        
        CallDeferred(nameof(DeferredSceneChange), BATTLE_SCENE);
    }
    
    private void DeferredSceneChange(string scenePath)
    {
        GetTree().ChangeSceneToFile(scenePath);
        isTransitioning = false;
    }
    
    private void DeferredSceneChangePacked(PackedScene packedScene)
    {
        GetTree().ChangeSceneToPacked(packedScene);
        isTransitioning = false;
    }
    
    private void OnSequenceComplete()
    {
        Log("[SceneManager] All sequences completed");
        // Could transition to main menu, credits, etc.
    }
    
    #endregion
    
    #region P Variable System
    
    public int GetP() => pValue;
    
    public void SetP(int value)
    {
        pValue = value;
        SyncPToDialogic();
    }
    
    public void AddP(int amount)
    {
        pValue += amount;
        SyncPToDialogic();
        Log($"[SceneManager] P changed by {amount} (total: {pValue})");
    }
    
    private void SyncPToDialogic()
    {
        if (dialogicAutoload == null) return;
        
        try
        {
            var varSystemVariant = dialogicAutoload.Get("VAR");
            if (varSystemVariant.VariantType == Variant.Type.Object)
            {
                var varSystem = varSystemVariant.AsGodotObject();
                varSystem?.Call("set_variable", "P", pValue);
                Log($"[SceneManager] Synced P to Dialogic: {pValue}");
            }
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"[SceneManager] Error syncing P: {e.Message}");
        }
    }
    
    public void LoadPFromDialogic()
    {
        if (dialogicAutoload == null) return;
        
        try
        {
            var varSystemVariant = dialogicAutoload.Get("VAR");
            if (varSystemVariant.VariantType == Variant.Type.Object)
            {
                var varSystem = varSystemVariant.AsGodotObject();
                if (varSystem != null)
                {
                    var pVar = varSystem.Call("get_variable", "P");
                    if (pVar.Obj != null)
                    {
                        pValue = pVar.AsInt32();
                        Log($"[SceneManager] Loaded P from Dialogic: {pValue}");
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"[SceneManager] Error loading P: {e.Message}");
        }
    }
    
    #endregion
    
    #region Battle Results
    
    public void StoreBattleResults(Dictionary<string, Variant> results)
    {
        battleResults.Clear();
        foreach (var kvp in results)
        {
            battleResults[kvp.Key] = kvp.Value;
        }
        
        Log($"[SceneManager] Stored {battleResults.Count} battle results");
    }
    
    public Dictionary<string, Variant> GetBattleResults() => 
        new Dictionary<string, Variant>(battleResults);
    
    public void ClearBattleResults()
    {
        battleResults.Clear();
    }
    
    #endregion
    
    #region Scene Parameters
    
    public Variant GetSceneParameter(string key, Variant defaultValue = default) =>
        sceneParameters.GetValueOrDefault(key, defaultValue);
    
    public bool HasSceneParameter(string key) => 
        sceneParameters.ContainsKey(key);
    
    #endregion
    
    #region Dialogic Integration
    
    private void OnTimelineEnded()
    {
        Log("[SceneManager] Timeline ended");
        LoadPFromDialogic();
        LoadNextInSequence();
    }
    
    private void OnDialogicSignal(string argument)
    {
        Log($"[SceneManager] Dialogic signal: {argument}");
        
        if (argument.StartsWith("add_p:"))
        {
            if (int.TryParse(argument.Substring(6), out int amount))
            {
                AddP(amount);
            }
        }
        else if (argument.StartsWith("jump:"))
        {
            string jumpId = argument.Substring(5);
            JumpToSequence(jumpId);
        }
    }
    
    #endregion
    
    #region Navigation & Debug
    
    public void JumpToSequence(string sequenceId)
    {
        int index = sequence.FindIndex(s => s.id == sequenceId);
        
        if (index >= 0)
        {
            sequenceIndex = index;
            GD.Print($"[SceneManager] Jumped to sequence: {sequenceId} (index {index})");
            LoadNextInSequence();
        }
        else
        {
            GD.PrintErr($"[SceneManager] Sequence not found: {sequenceId}");
        }
    }
    
    public void JumpToSequenceIndex(int index)
    {
        if (index < 0 || index >= sequence.Count)
        {
            GD.PrintErr($"[SceneManager] Invalid index: {index}");
            return;
        }
        
        sequenceIndex = index;
        GD.Print($"[SceneManager] Jumped to index {index}");
        LoadNextInSequence();
    }
    
    public void RestartSequence()
    {
        sequenceIndex = 0;
        pValue = 0;
        battleResults.Clear();
        SyncPToDialogic();
        
        GD.Print("[SceneManager] Sequence restarted");
        LoadNextInSequence();
    }
    
    private void PrintSequenceList()
    {
        GD.Print("=== SEQUENCE LIST ===");
        for (int i = 0; i < sequence.Count; i++)
        {
            var entry = sequence[i];
            string marker = i == testStartIndex ? " <-- START" : "";
            GD.Print($"  [{i}] {entry.id} ({entry.type}){marker}");
        }
        GD.Print("=====================");
    }
    
    public void PrintCurrentState()
    {
        GD.Print("=== SCENE MANAGER STATE ===");
        GD.Print($"  Test Mode: {testMode}");
        GD.Print($"  Sequence Index: {sequenceIndex}/{sequence.Count}");
        GD.Print($"  P Value: {pValue}");
        GD.Print($"  Battle Results: {battleResults.Count}");
        GD.Print($"  Transitioning: {isTransitioning}");
        
        if (sequenceIndex > 0 && sequenceIndex <= sequence.Count)
        {
            var current = sequence[sequenceIndex - 1];
            GD.Print($"  Current: {current.id} ({current.type})");
        }
        
        GD.Print("===========================");
    }
    
    private void Log(string message)
    {
        if (verboseLogging)
        {
            GD.Print(message);
        }
    }
    
    #endregion
    
    #region Data Structures
    
    private class SequenceData
    {
        public List<SequenceEntry> sequences { get; set; }
    }
    
    private class SequenceEntry
    {
        public string id { get; set; } = "";
        public string type { get; set; } = "";
        public string timeline { get; set; } = "";
        public string config { get; set; } = "";
        public string map { get; set; } = "";
        public string data { get; set; } = "";
    }
    
    #endregion
}