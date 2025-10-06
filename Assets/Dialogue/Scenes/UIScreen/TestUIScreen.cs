// Attach to test-03-1000-results.tscn and test-05-1600-gameover.tscn
using Godot;

public partial class TestUIScreen : Control
{
    public override void _Ready()
    {
        var sceneManager = GetNode<SceneManager>("/root/SceneManager");
        
        // Display data if present
        if (sceneManager.HasSceneParameter("data"))
        {
            var data = sceneManager.GetSceneParameter("data").AsGodotDictionary();
            GetNode<Label>("Label").Text = data["title"].AsString();
        }
        
        // Button advances sequence
        GetNode<Button>("Button").Pressed += () => sceneManager.LoadNextInSequence();
    }
}