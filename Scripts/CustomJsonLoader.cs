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
        public List<EntityDefinition> Entities { get; set; } = new();
        public GameSettings Settings { get; set; } = new();
    }
    
    public class EntityDefinition
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string EntityType { get; set; } = "Enemy"; // Player, Ally, Enemy, NPC, Neutral
        public float MaxHP { get; set; } = 100;
        public int Initiative { get; set; } = 50;
        public int Speed { get; set; } = 5;
        public Vector2IData StartPosition { get; set; } = new();
        public List<string> AvailableSkills { get; set; } = new();
        public List<string> AvailableItems { get; set; } = new();
        public NPCBehaviorConfig BehaviorConfig { get; set; } = new();
    }
    
    public class NPCBehaviorConfig
    {
        public string BehaviorType { get; set; } = "balanced";
        public int AggressionLevel { get; set; } = 5;
        public int CautiousnessLevel { get; set; } = 5;
        public float HealthThreshold { get; set; } = 0.3f;
        
        public int AttackPriority { get; set; } = 5;
        public int DefendPriority { get; set; } = 5;
        public int SupportPriority { get; set; } = 5;
        public int MovePriority { get; set; } = 5;
        
        public List<string> PreferredTargets { get; set; } = new();
        public bool AvoidFriendlyFire { get; set; } = true;
        public bool PreferGroupedTargets { get; set; } = false;
        
        public List<string> PreferredSkills { get; set; } = new();
        public List<string> EmergencySkills { get; set; } = new();
        public int MinRangePreference { get; set; } = 0;
        public int MaxRangePreference { get; set; } = 10;
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

            if (config.Skills.Count == 0 && config.Items.Count == 0 && config.MoveOptions.Count == 0 && config.TalkOptions.Count == 0)
            {
                GD.PrintErr("Configuration file appears to be empty or invalid. Please ensure battle_config.json is properly formatted.");
                return config;
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