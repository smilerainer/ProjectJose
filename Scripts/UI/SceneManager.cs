// SceneManager.cs - Data-driven scene sequencing
using Godot;
using System.Collections.Generic;
using System.Text.Json;

public partial class SceneManager : Node
{
    #region Configuration
    
    private const string SEQUENCE_DATA_PATH = "res://data/story_sequence.json";
    private const string VN_SCENE_TEMPLATE = "res://Assets/Dialogue/Scenes/test-01-0800-intro.tscn";
    
    [Export] private bool testMode = true;
    [Export] private int testStartIndex = 0;
    
    #endregion
    
    #region State
    
    private Node dialogicAutoload;
    private Dictionary<string, Variant> sceneParameters = new();
    private Dictionary<string, Variant> tempBattleResults = new();
    private List<SequenceEntry> sequence = new();
    private int sequenceIndex = 0;
    private int pValue = 0;
    
    #endregion
    
    #region Initialization
    
    public override void _Ready()
    {
        dialogicAutoload = GetNodeOrNull("/root/Dialogic");
        
        if (dialogicAutoload != null)
        {
            dialogicAutoload.Connect("timeline_ended", new Callable(this, nameof(OnTimelineEnded)));
            dialogicAutoload.Connect("signal_event", new Callable(this, nameof(OnDialogicSignal)));
            
            // Initialize P variable in Dialogic
            SyncPToDialogic();
        }
        
        GD.Print("[SceneManager] Initialized");
        LoadSequence(SEQUENCE_DATA_PATH);
    }
    
    #endregion
    
    #region Sequence Loading
    
    private void LoadSequence(string jsonPath)
    {
        using var file = FileAccess.Open(jsonPath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PrintErr($"[SceneManager] Cannot load: {jsonPath}");
            return;
        }
        
        string json = file.GetAsText();
        var data = JsonSerializer.Deserialize<SequenceData>(json);
        
        if (data?.sequences == null)
        {
            GD.PrintErr("[SceneManager] Invalid sequence data");
            return;
        }
        
        sequence = data.sequences;
        sequenceIndex = testMode ? testStartIndex : 0;
        
        if (testMode)
            GD.Print($"[SceneManager] TEST MODE: Starting at index {testStartIndex}");
        
        GD.Print($"[SceneManager] Loaded {sequence.Count} sequence entries");
        
        if (testMode)
            PrintSequenceList();
        
        LoadNextInSequence();
    }
    
    public void LoadNextInSequence()
    {
        if (sequenceIndex >= sequence.Count)
        {
            GD.Print("[SceneManager] Sequence complete");
            return;
        }
        
        var entry = sequence[sequenceIndex++];
        GD.Print($"[SceneManager] Loading [{sequenceIndex}/{sequence.Count}] {entry.id} ({entry.type})");
        
        sceneParameters.Clear();
        
        // Pass custom data if present
        if (!string.IsNullOrEmpty(entry.data))
        {
            try
            {
                var dataDict = JsonSerializer.Deserialize<Dictionary<string, object>>(entry.data);
                var godotDict = new Godot.Collections.Dictionary();
                foreach (var kvp in dataDict)
                    godotDict[kvp.Key] = kvp.Value.ToString();
                sceneParameters["data"] = godotDict;
            }
            catch
            {
                GD.PrintErr($"[SceneManager] Failed to parse data for {entry.id}");
            }
        }
        
        switch (entry.type)
        {
            case "vn":
                sceneParameters["timeline"] = entry.timeline;
                GetTree().CallDeferred("change_scene_to_file", VN_SCENE_TEMPLATE);
                break;
                
            case "battle":
                sceneParameters["battle_config"] = entry.config;
                GetTree().CallDeferred("change_scene_to_file", entry.scene);
                break;
                
            case "ui":
                GetTree().CallDeferred("change_scene_to_file", entry.scene);
                break;
        }
    }
    
    #endregion
    
    #region P Variable
    
    public int GetP() => pValue;
    public void SetP(int value) { pValue = value; SyncPToDialogic(); }
    public void AddP(int amount) { pValue += amount; SyncPToDialogic(); }
    
    private void SyncPToDialogic()
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
                    // Dialogic's set_variable will create the variable if it doesn't exist
                    // So we can safely call it without checking first
                    varSystem.Call("set_variable", "P", pValue);
                    
                    // Verify it was set
                    var verifyVar = varSystem.Call("get_variable", "P");
                    if (verifyVar.Obj != null)
                    {
                        GD.Print($"[SceneManager] Synced P to Dialogic: {pValue}");
                    }
                    else
                    {
                        GD.PrintErr($"[SceneManager] Failed to set P variable in Dialogic");
                    }
                }
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
                    // Use Dialogic's VAR.get_variable() method correctly
                    var pVar = varSystem.Call("get_variable", "P");
                    if (pVar.Obj != null)
                    {
                        pValue = pVar.AsInt32();
                        GD.Print($"[SceneManager] Loaded P from Dialogic: {pValue}");
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
        tempBattleResults.Clear();
        foreach (var kvp in results)
            tempBattleResults[kvp.Key] = kvp.Value;
    }
    
    public Dictionary<string, Variant> GetBattleResults() => 
        new Dictionary<string, Variant>(tempBattleResults);
    
    #endregion
    
    #region Scene Parameters
    
    public Variant GetSceneParameter(string key, Variant defaultValue = default) =>
        sceneParameters.GetValueOrDefault(key, defaultValue);
    
    public bool HasSceneParameter(string key) => sceneParameters.ContainsKey(key);
    
    #endregion
    
    #region Dialogic Integration
    
    private void OnTimelineEnded()
    {
        LoadPFromDialogic();
        LoadNextInSequence();
    }
    
    private void OnDialogicSignal(string argument)
    {
        if (argument.StartsWith("add_p:"))
        {
            if (int.TryParse(argument.Substring(6), out int amount))
                AddP(amount);
        }
    }
    
    #endregion
    
    #region Testing & Debug
    
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
    
    public void PrintCurrentState()
    {
        GD.Print("=== SCENE MANAGER STATE ===");
        GD.Print($"  Test Mode: {testMode}");
        GD.Print($"  Sequence: {sequenceIndex}/{sequence.Count}");
        GD.Print($"  P Value: {pValue}");
        GD.Print($"  Scene Parameters: {sceneParameters.Count}");
        if (sequenceIndex > 0 && sequenceIndex <= sequence.Count)
        {
            var current = sequence[sequenceIndex - 1];
            GD.Print($"  Current: {current.id} ({current.type})");
        }
        GD.Print("===========================");
    }
    
    #endregion
    
    #region Data Structures
    
    private class SequenceData
    {
        public List<SequenceEntry> sequences { get; set; }
    }
    
    private class SequenceEntry
    {
        public string id { get; set; }
        public string type { get; set; }
        public string timeline { get; set; }
        public string scene { get; set; }
        public string config { get; set; }
        public string data { get; set; }
    }
    
    #endregion
}