using Godot;
using System.Text.Json;

public partial class CodeTest : Node
{
    // Simple test data structure
    public class TestData
    {
        public string Name { get; set; } = "";
        public int Level { get; set; } = 0;
        public float Health { get; set; } = 0f;
        public bool IsActive { get; set; } = false;
    }

    public override void _Ready()
    {
        TestBasicJsonSaveLoad();
    }

    private void TestBasicJsonSaveLoad()
    {
        GD.Print("=== Testing Basic JSON Save/Load ===");
        GD.Print(OS.GetDataDir());

        // 1. Create test data
        var originalData = new TestData
        {
            Name = "TestPlayer",
            Level = 5,
            Health = 85.5f,
            IsActive = true
        };

        GD.Print($"Original Data: Name={originalData.Name}, Level={originalData.Level}, Health={originalData.Health}, Active={originalData.IsActive}");

        try
        {
            // 2. Convert to JSON string
            string jsonString = JsonSerializer.Serialize(originalData, new JsonSerializerOptions { WriteIndented = true });
            GD.Print($"JSON String:\n{jsonString}");

            // 3. Convert back from JSON
            var loadedData = JsonSerializer.Deserialize<TestData>(jsonString);
            GD.Print($"Loaded Data: Name={loadedData.Name}, Level={loadedData.Level}, Health={loadedData.Health}, Active={loadedData.IsActive}");

            // 4. Verify data matches
            bool success = originalData.Name == loadedData.Name &&
                          originalData.Level == loadedData.Level &&
                          originalData.Health == loadedData.Health &&
                          originalData.IsActive == loadedData.IsActive;

            GD.Print($"Test Result: {(success ? "SUCCESS" : "FAILED")}");

            // 5. Test file save/load
            TestFileSaveLoad(originalData);
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"JSON Test Failed: {ex.Message}");
        }
    }

    private void TestFileSaveLoad(TestData data)
    {
        GD.Print("\n=== Testing File Save/Load ===");

        string filePath = "user://test_save.json";

        try
        {
            // Save to file
            string jsonString = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
            if (file != null)
            {
                file.StoreString(jsonString);
                file.Close();
                GD.Print($"Saved to: {filePath}");
            }
            else
            {
                GD.PrintErr("Failed to create save file");
                return;
            }

            // Load from file
            using var loadFile = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
            if (loadFile != null)
            {
                string loadedJson = loadFile.GetAsText();
                loadFile.Close();
                
                var loadedData = JsonSerializer.Deserialize<TestData>(loadedJson);
                GD.Print($"Loaded from file: Name={loadedData.Name}, Level={loadedData.Level}");

                // Test modification
                loadedData.Level = 99;
                loadedData.Name = "ModifiedPlayer";
                GD.Print($"Modified: Name={loadedData.Name}, Level={loadedData.Level}");

                GD.Print("File Save/Load: SUCCESS");
            }
            else
            {
                GD.PrintErr("Failed to load save file");
            }
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"File Test Failed: {ex.Message}");
        }
    }

    // Call this method from _input or connect to a button
    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_accept")) // Space/Enter
        {
            GD.Print("\n=== Running JSON Test Again ===");
            TestBasicJsonSaveLoad();
        }
    }
}