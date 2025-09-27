// CustomJsonLoader.cs - Generic JSON loading and saving system
using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CustomJsonSystem
{
    #region Battle Configuration Data
    
    public class BattleConfigData
    {
        public List<ActionConfig> Skills { get; set; } = new();
        public List<ActionConfig> Items { get; set; } = new();
        public List<ActionConfig> TalkOptions { get; set; } = new();
        public List<ActionConfig> MoveOptions { get; set; } = new();
        public GameSettings Settings { get; set; } = new();
    }
    
    public class ActionConfig
    {
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
        public int UsesRemaining { get; set; } = -1; // -1 = unlimited
        public List<PatternCell> RangePattern { get; set; } = new();
        public List<PatternCell> AoePattern { get; set; } = new();
        public Dictionary<string, object> CustomProperties { get; set; } = new();
    }
    
    public class PatternCell
    {
        public int X { get; set; } = 0;
        public int Y { get; set; } = 0;
        
        public Vector2I ToVector2I() => new Vector2I(X, Y);
        public static PatternCell FromVector2I(Vector2I vector) => new() { X = vector.X, Y = vector.Y };
    }
    
    public class GameSettings
    {
        public int StartingMoney { get; set; } = 100;
        public int MaxPartySize { get; set; } = 4;
        public int DefaultMoveRange { get; set; } = 1;
        public bool EnableFriendlyFire { get; set; } = false;
        public Dictionary<string, object> CustomSettings { get; set; } = new();
    }
    
    #endregion
    
    #region Save Data Structures
    
    public class GameSaveData
    {
        public SaveMetadata Metadata { get; set; } = new();
        public PlayerData Player { get; set; } = new();
        public BattleStateData BattleState { get; set; } = new();
        public List<EntityData> Entities { get; set; } = new();
        public Dictionary<string, object> CustomData { get; set; } = new();
    }
    
    public class SaveMetadata
    {
        public string SaveVersion { get; set; } = "1.0";
        public DateTime SaveTime { get; set; } = DateTime.Now;
        public string PlayerName { get; set; } = "";
        public int SaveSlot { get; set; } = 0;
        public string SceneName { get; set; } = "";
        public float PlayTime { get; set; } = 0f;
    }
    
    public class PlayerData
    {
        public string Name { get; set; } = "";
        public int Level { get; set; } = 1;
        public float HP { get; set; } = 100;
        public float MaxHP { get; set; } = 100;
        public int Money { get; set; } = 100;
        public Vector2IData Position { get; set; } = new();
        public Dictionary<string, int> Inventory { get; set; } = new();
        public Dictionary<string, bool> Flags { get; set; } = new();
    }
    
    public class BattleStateData
    {
        public int Round { get; set; } = 0;
        public bool IsActive { get; set; } = false;
        public string CurrentPhase { get; set; } = "";
        public Dictionary<string, object> StateData { get; set; } = new();
    }
    
    public class EntityData
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public float HP { get; set; } = 100;
        public float MaxHP { get; set; } = 100;
        public Vector2IData Position { get; set; } = new();
        public Dictionary<string, int> StatusEffects { get; set; } = new();
        public Dictionary<string, object> Properties { get; set; } = new();
    }
    
    public class Vector2IData
    {
        public int X { get; set; } = 0;
        public int Y { get; set; } = 0;
        
        public Vector2I ToVector2I() => new Vector2I(X, Y);
        public static Vector2IData FromVector2I(Vector2I vector) => new() { X = vector.X, Y = vector.Y };
    }
    
    #endregion
    
    #region Generic JSON Loader
    
    public static class CustomJsonLoader
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        
        #region Generic Load/Save Methods
        
        public static T LoadFromFile<T>(string filePath) where T : class, new()
        {
            try
            {
                using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
                if (file == null)
                {
                    GD.PrintErr($"File not found: {filePath}");
                    return new T();
                }
                
                string json = file.GetAsText();
                file.Close();
                
                var result = JsonSerializer.Deserialize<T>(json, JsonOptions);
                GD.Print($"Loaded {typeof(T).Name} from: {filePath}");
                return result ?? new T();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Failed to load {typeof(T).Name} from {filePath}: {ex.Message}");
                return new T();
            }
        }
        
        public static void SaveToFile<T>(T data, string filePath) where T : class
        {
            try
            {
                string json = JsonSerializer.Serialize(data, JsonOptions);
                using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
                if (file != null)
                {
                    file.StoreString(json);
                    file.Close();
                    GD.Print($"Saved {typeof(T).Name} to: {filePath}");
                }
                else
                {
                    GD.PrintErr($"Failed to create file: {filePath}");
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Failed to save {typeof(T).Name}: {ex.Message}");
            }
        }
        
        public static string ToJsonString<T>(T data) where T : class
        {
            try
            {
                return JsonSerializer.Serialize(data, JsonOptions);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Failed to serialize {typeof(T).Name}: {ex.Message}");
                return "{}";
            }
        }
        
        public static T FromJsonString<T>(string json) where T : class, new()
        {
            try
            {
                return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? new T();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Failed to deserialize {typeof(T).Name}: {ex.Message}");
                return new T();
            }
        }
        
        public static bool FileExists(string filePath)
        {
            return FileAccess.FileExists(filePath);
        }
        
        public static bool ValidateJsonFile<T>(string filePath) where T : class, new()
        {
            try
            {
                LoadFromFile<T>(filePath);
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        #endregion
        
        #region Specific Helper Methods
        
        public static BattleConfigData LoadBattleConfig(string filePath)
        {
            var config = LoadFromFile<BattleConfigData>(filePath);
            if (config.Skills.Count == 0 && config.Items.Count == 0)
            {
                GD.Print("Empty config loaded, creating default config");
                return CreateDefaultBattleConfig();
            }
            return config;
        }
        
        public static GameSaveData LoadGameSave(string filePath)
        {
            return LoadFromFile<GameSaveData>(filePath);
        }
        
        public static void SaveGameData(GameSaveData saveData, string filePath)
        {
            saveData.Metadata.SaveTime = DateTime.Now;
            SaveToFile(saveData, filePath);
        }
        
        public static BattleConfigData CreateDefaultBattleConfig()
        {
            return new BattleConfigData
            {
                Skills = new List<ActionConfig>
                {
                    new() { Id = "fireball", Name = "Fireball", Description = "A blazing projectile", Damage = 25, Cost = 5, Range = 2 },
                    new() { Id = "ice_thorn", Name = "Ice Thorn", Description = "Freezing spike", Damage = 20, Cost = 4, StatusEffect = "Slow", StatusDuration = 2 },
                    new() { Id = "shroud", Name = "Shroud", Description = "Protective darkness", Cost = 3, StatusEffect = "Stealth", StatusDuration = 3, TargetType = "Self" }
                },
                Items = new List<ActionConfig>
                {
                    new() { Id = "potion", Name = "Potion", Description = "Restores health", HealAmount = 50, UsesRemaining = 3 },
                    new() { Id = "ether", Name = "Ether", Description = "Restores energy", Cost = -10, UsesRemaining = 2, TargetType = "Self" },
                    new() { Id = "phoenix_down", Name = "Phoenix Down", Description = "Revives fallen ally", HealAmount = 1, StatusEffect = "Revive", UsesRemaining = 1 }
                },
                TalkOptions = new List<ActionConfig>
                {
                    new() { Id = "negotiate", Name = "Negotiate", Description = "Attempt peaceful resolution", Dialogue = "Let's talk this through...", FriendshipChange = 10 },
                    new() { Id = "intimidate", Name = "Intimidate", Description = "Show dominance", Dialogue = "You don't want to fight me!", ReputationChange = 5, FriendshipChange = -5 },
                    new() { Id = "flee", Name = "Flee", Description = "Attempt to escape", Dialogue = "This isn't worth it!", Cost = 5 }
                },
                MoveOptions = new List<ActionConfig>
                {
                    new() { Id = "move", Name = "Move", Description = "Move to adjacent cell", Range = 1 }
                },
                Settings = new GameSettings
                {
                    StartingMoney = 100,
                    MaxPartySize = 4,
                    DefaultMoveRange = 1,
                    EnableFriendlyFire = false
                }
            };
        }
        
        public static GameSaveData CreateNewGameSave(string playerName = "Player")
        {
            return new GameSaveData
            {
                Metadata = new SaveMetadata
                {
                    PlayerName = playerName,
                    SaveTime = DateTime.Now,
                    SceneName = "Battle"
                },
                Player = new PlayerData
                {
                    Name = playerName,
                    HP = 100,
                    MaxHP = 100,
                    Money = 100,
                    Position = Vector2IData.FromVector2I(new Vector2I(0, 0))
                },
                BattleState = new BattleStateData
                {
                    Round = 0,
                    IsActive = false,
                    CurrentPhase = "MainMenu"
                }
            };
        }
        
        #endregion
    }
    
    #endregion
}