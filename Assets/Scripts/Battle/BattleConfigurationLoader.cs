// BattleConfigurationLoader.cs - Loads and manages battle configuration
using Godot;
using System.Collections.Generic;
using System.Linq;
using CustomJsonSystem;

public class BattleConfigurationLoader
{
    #region Configuration Data
    
    private BattleConfigData battleConfig;
    private Dictionary<string, ActionConfig> allActions = new();
    private Dictionary<string, ActionConfig> actionsByName = new();
    private bool configLoaded = false;
    
    #endregion
    
    #region Configuration Loading
    
    public bool LoadConfiguration(string configFilePath)
    {
        try
        {
            GD.Print($"[BattleConfig] Loading configuration from: {configFilePath}");
            
            battleConfig = CustomJsonLoader.LoadBattleConfig(configFilePath);
            
            if (battleConfig != null)
            {
                BuildActionLookups();
                configLoaded = true;
                
                LogConfigurationSummary();
                return true;
            }
            else
            {
                GD.PrintErr("[BattleConfig] Loaded battleConfig is null");
                configLoaded = false;
                return false;
            }
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"[BattleConfig] Failed to load configuration: {e.Message}");
            GD.PrintErr($"[BattleConfig] Stack trace: {e.StackTrace}");
            configLoaded = false;
            return false;
        }
    }
    
    private void BuildActionLookups()
    {
        allActions.Clear();
        actionsByName.Clear();
        
        // Build lookup by ID
        foreach (var skill in battleConfig.Skills)
        {
            allActions[skill.Id] = skill;
            actionsByName[skill.Name] = skill;
        }
        
        foreach (var item in battleConfig.Items)
        {
            allActions[item.Id] = item;
            actionsByName[item.Name] = item;
        }
        
        foreach (var talk in battleConfig.TalkOptions)
        {
            allActions[talk.Id] = talk;
            actionsByName[talk.Name] = talk;
        }
        
        foreach (var move in battleConfig.MoveOptions)
        {
            allActions[move.Id] = move;
            actionsByName[move.Name] = move;
        }
        
        GD.Print($"[BattleConfig] Built action lookups - {allActions.Count} total actions");
    }
    
    private void LogConfigurationSummary()
    {
        GD.Print("═══ BATTLE CONFIGURATION LOADED ═══");
        GD.Print($"Skills: {battleConfig.Skills.Count}");
        foreach (var skill in battleConfig.Skills)
        {
            GD.Print($"  - {skill.Name} (ID: {skill.Id}, Damage: {skill.Damage}, Cost: {skill.Cost})");
        }
        
        GD.Print($"Items: {battleConfig.Items.Count}");
        foreach (var item in battleConfig.Items)
        {
            GD.Print($"  - {item.Name} (ID: {item.Id}, Heal: {item.HealAmount}, Uses: {item.UsesRemaining})");
        }
        
        GD.Print($"Talk Options: {battleConfig.TalkOptions.Count}");
        foreach (var talk in battleConfig.TalkOptions)
        {
            GD.Print($"  - {talk.Name} (ID: {talk.Id}, Friendship: {talk.FriendshipChange})");
        }
        
        GD.Print($"Move Options: {battleConfig.MoveOptions.Count}");
        foreach (var move in battleConfig.MoveOptions)
        {
            GD.Print($"  - {move.Name} (ID: {move.Id}, Range: {move.Range})");
        }
        
        GD.Print("═══════════════════════════════════");
    }
    
    #endregion
    
    #region Configuration Access
    
    public bool IsConfigLoaded() => configLoaded;
    
    public BattleConfigData GetBattleConfig() => battleConfig;
    
    public ActionConfig GetActionConfig(string actionName)
    {
        if (!configLoaded)
        {
            GD.PrintErr("[BattleConfig] Cannot get action config - configuration not loaded");
            return null;
        }
        
        if (actionsByName.TryGetValue(actionName, out ActionConfig action))
        {
            GD.Print($"[BattleConfig] Found action config for: {actionName}");
            return action;
        }
        
        GD.PrintErr($"[BattleConfig] Action config not found for: '{actionName}'");
        GD.PrintErr($"[BattleConfig] Available actions: [{string.Join(", ", actionsByName.Keys)}]");
        return null;
    }
    
    public ActionConfig GetActionConfigById(string actionId)
    {
        if (!configLoaded)
        {
            GD.PrintErr("[BattleConfig] Cannot get action config - configuration not loaded");
            return null;
        }
        
        if (allActions.TryGetValue(actionId, out ActionConfig action))
        {
            return action;
        }
        
        GD.PrintErr($"[BattleConfig] Action config not found for ID: '{actionId}'");
        return null;
    }
    
    #endregion
    
    #region Action Type Access
    
    public string[] GetSkillNames()
    {
        if (!configLoaded || battleConfig?.Skills == null)
        {
            GD.PrintWarn("[BattleConfig] Cannot get skill names - configuration not loaded properly");
            return new string[0];
        }
        
        return battleConfig.Skills.Select(s => s.Name).ToArray();
    }
    
    public string[] GetItemNames()
    {
        if (!configLoaded || battleConfig?.Items == null)
        {
            GD.PrintWarn("[BattleConfig] Cannot get item names - configuration not loaded properly");
            return new string[0];
        }
        
        return battleConfig.Items.Select(i => i.Name).ToArray();
    }
    
    public string[] GetTalkOptionNames()
    {
        if (!configLoaded || battleConfig?.TalkOptions == null)
        {
            GD.PrintWarn("[BattleConfig] Cannot get talk option names - configuration not loaded properly");
            return new string[0];
        }
        
        return battleConfig.TalkOptions.Select(t => t.Name).ToArray();
    }
    
    public string[] GetMoveOptionNames()
    {
        if (!configLoaded || battleConfig?.MoveOptions == null)
        {
            GD.PrintWarn("[BattleConfig] Cannot get move option names - configuration not loaded properly");
            return new string[0];
        }
        
        return battleConfig.MoveOptions.Select(m => m.Name).ToArray();
    }
    
    #endregion
    
    #region Filtered Access
    
    public List<ActionConfig> GetSkillsForEntity(string entityType)
    {
        if (!configLoaded) return new List<ActionConfig>();
        
        // TODO: Filter skills based on entity type, level, etc.
        // For now, return all skills
        return battleConfig.Skills.ToList();
    }
    
    public List<ActionConfig> GetItemsForEntity(string entityType)
    {
        if (!configLoaded) return new List<ActionConfig>();
        
        // TODO: Filter items based on inventory, entity type, etc.
        // For now, return all items with remaining uses
        return battleConfig.Items.Where(i => i.UsesRemaining > 0 || i.UsesRemaining == -1).ToList();
    }
    
    public List<ActionConfig> GetTalkOptionsForEntity(string entityType)
    {
        if (!configLoaded) return new List<ActionConfig>();
        
        // TODO: Filter talk options based on entity relationships, context, etc.
        // For now, return all talk options
        return battleConfig.TalkOptions.ToList();
    }
    
    public List<ActionConfig> GetMoveOptionsForEntity(string entityType)
    {
        if (!configLoaded) return new List<ActionConfig>();
        
        // TODO: Filter move options based on entity capabilities, terrain, etc.
        // For now, return all move options
        return battleConfig.MoveOptions.ToList();
    }
    
    #endregion
    
    #region Validation
    
    public bool ValidateConfiguration()
    {
        if (!configLoaded)
        {
            GD.PrintErr("[BattleConfig] Cannot validate - configuration not loaded");
            return false;
        }
        
        var issues = new List<string>();
        
        // Validate that all actions have unique IDs
        var allIds = new HashSet<string>();
        foreach (var action in allActions.Values)
        {
            if (!allIds.Add(action.Id))
            {
                issues.Add($"Duplicate action ID: {action.Id}");
            }
        }
        
        // Validate that all actions have names
        foreach (var action in allActions.Values)
        {
            if (string.IsNullOrEmpty(action.Name))
            {
                issues.Add($"Action with ID {action.Id} has no name");
            }
        }
        
        // Log any issues found
        if (issues.Count > 0)
        {
            GD.PrintErr("[BattleConfig] Configuration validation failed:");
            foreach (var issue in issues)
            {
                GD.PrintErr($"  - {issue}");
            }
            return false;
        }
        
        GD.Print("[BattleConfig] Configuration validation passed");
        return true;
    }
    
    #endregion
    
    #region Debug Methods
    
    public void PrintAllActions()
    {
        if (!configLoaded)
        {
            GD.Print("[BattleConfig] Cannot print actions - configuration not loaded");
            return;
        }
        
        GD.Print("═══ ALL LOADED ACTIONS ═══");
        foreach (var kvp in actionsByName)
        {
            var action = kvp.Value;
            GD.Print($"{action.Name} (ID: {action.Id})");
            GD.Print($"  Type: Skills={battleConfig.Skills.Contains(action)}, Items={battleConfig.Items.Contains(action)}, Talk={battleConfig.TalkOptions.Contains(action)}, Move={battleConfig.MoveOptions.Contains(action)}");
            GD.Print($"  Range: {action.Range}, Cost: {action.Cost}");
            GD.Print($"  Target: {action.TargetType}");
        }
        GD.Print("═══════════════════════");
    }
    
    #endregion
}