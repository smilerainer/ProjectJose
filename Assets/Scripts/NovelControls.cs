// NovelControls.cs - Simple visual novel engine (placeholder for future implementation)
using Godot;

public partial class NovelControls : Control
{
    [Signal] public delegate void TextAdvancedEventHandler();
    [Signal] public delegate void ChoiceMadeEventHandler(int choiceIndex);
    [Signal] public delegate void NovelActivatedEventHandler();
    [Signal] public delegate void NovelDeactivatedEventHandler();
    
    [Export] private RichTextLabel textDisplay;
    [Export] private Container choicesContainer;
    [Export] private bool autoAdvanceOnClick = true;
    
    private bool isActive = false;
    private bool isShowingChoices = false;
    
    public bool IsActive => isActive;
    public bool IsShowingChoices => isShowingChoices;
    public Vector2 CurrentActionPosition => Vector2.Zero; // TODO: Implement based on current state
    
    #region Public API
    
    public override void _Ready()
    {
        InitializeNovel();
    }
    
    public void SetActive(bool active)
    {
        if (isActive == active) return;
        
        isActive = active;
        
        if (active)
            ActivateNovel();
        else
            DeactivateNovel();
    }
    
    public void Navigate(Vector2I direction)
    {
        if (!isActive) return;
        
        // TODO: Implement choice navigation when choices are shown
        if (isShowingChoices)
        {
            NavigateChoices(direction);
        }
    }
    
    public void AdvanceText()
    {
        if (!isActive) return;
        
        // TODO: Implement text advancement
        EmitSignal(SignalName.TextAdvanced);
    }
    
    public void MakeChoice(int choiceIndex)
    {
        if (!isActive || !isShowingChoices) return;
        
        // TODO: Implement choice selection
        EmitSignal(SignalName.ChoiceMade, choiceIndex);
    }
    
    #endregion
    
    #region Novel Management (TODO)
    
    private void InitializeNovel()
    {
        FindComponents();
        SetupInitialState();
    }
    
    private void FindComponents()
    {
        textDisplay ??= GetNodeOrNull<RichTextLabel>("TextDisplay");
        choicesContainer ??= GetNodeOrNull<Container>("ChoicesContainer");
        
        if (textDisplay == null)
            GD.PrintErr($"[NovelControls] No text display found in {Name}");
    }
    
    private void SetupInitialState()
    {
        isActive = false;
        isShowingChoices = false;
    }
    
    private void ActivateNovel()
    {
        EmitSignal(SignalName.NovelActivated);
        GD.Print($"[NovelControls] Activated novel: {Name}");
    }
    
    private void DeactivateNovel()
    {
        EmitSignal(SignalName.NovelDeactivated);
        GD.Print($"[NovelControls] Deactivated novel: {Name}");
    }
    
    private void NavigateChoices(Vector2I direction)
    {
        // TODO: Implement choice navigation
        GD.Print("[NovelControls] Choice navigation - TODO");
    }
    
    #endregion
}