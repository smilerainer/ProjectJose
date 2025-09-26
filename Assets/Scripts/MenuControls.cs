// MenuControls.cs - Dead simple 2x2 grid
using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class MenuControls : Control
{
    [Signal] public delegate void ButtonSelectedEventHandler(int index, BaseButton button);
    [Signal] public delegate void ButtonActivatedEventHandler(int index, BaseButton button);
    [Signal] public delegate void MenuActivatedEventHandler();
    [Signal] public delegate void MenuDeactivatedEventHandler();
    [Export] private Node2D centralInputManager;

    
    [Export] private bool wrapNavigation = true;
    [Export] private bool autoActivateOnShow = true;
    
    private Container buttonContainer;
    private List<BaseButton> buttons = new();
    private int currentRow = 0;
    private int currentCol = 0;
    private bool isActive = false;
    
    // Simple 2x2 grid - buttons[0]=move, buttons[1]=skill, buttons[2]=item, buttons[3]=talk
    // Grid layout:
    // [0] [1]  ->  move  skill
    // [2] [3]  ->  item  talk
    
    public bool IsActive => isActive;
    public BaseButton CurrentButton => GetButtonAt(currentRow, currentCol);
    public Vector2 CurrentButtonPosition => CurrentButton?.GlobalPosition ?? Vector2.Zero;
    
    public override void _Ready()
    {
        InitializeMenu();
    }
    
    #region Public API
    
    public void SetActive(bool active)
    {
        if (isActive == active) return;
        isActive = active;
        
        if (active)
            ActivateMenu();
        else
            DeactivateMenu();
    }
    
    public void Navigate(Vector2I direction)
{
    if (!isActive || buttons.Count != 4) return;
    
    int newRow = currentRow;
    int newCol = currentCol;
    
    if (direction.Y > 0) newRow++; // Down
    else if (direction.Y < 0) newRow--; // Up
    else if (direction.X > 0) newCol++; // Right
    else if (direction.X < 0) newCol--; // Left
    
    // Check bounds and wrapping
    if (wrapNavigation)
    {
        newRow = (newRow + 2) % 2; // Wrap between 0 and 1
        newCol = (newCol + 2) % 2; // Wrap between 0 and 1
    }
    else
    {
        newRow = Mathf.Clamp(newRow, 0, 1);
        newCol = Mathf.Clamp(newCol, 0, 1);
    }

    // Only move if position actually changed
    if (newRow != currentRow || newCol != currentCol)
    {
        GD.Print($"[MenuControls] Navigate: ({currentRow},{currentCol}) -> ({newRow},{newCol})");
        currentRow = newRow;
        currentCol = newCol;
        ApplySelection();
        
        // Fix the node path and remove duplicate call
        var centralManager = GetNodeOrNull<CentralInputManager>("/TestBattle2/CentralInputManager");
        if (centralManager != null)
        {
            centralManager.NotifyButtonFocusChanged();
            GD.Print("✅ NotifyButtonFocusChanged() called successfully");
        }
        else
        {
            GD.PrintErr("❌ CentralInputManager not found at /TestBattle2/CentralInputManager");
        }
    }
    else
    {
        GD.Print($"[MenuControls] Navigate: ({currentRow},{currentCol}) - no movement (blocked or same position)");
    }
}
    
    public void ActivateCurrentButton()
    {
        var button = CurrentButton;
        if (button == null) return;
        
        button.EmitSignal(BaseButton.SignalName.Pressed);
        EmitSignal(SignalName.ButtonActivated, GetLinearIndex(), button);
    }
    public void ResetToFirstButton()
{
    currentRow = 0;
    currentCol = 0;
    ApplySelection();
    
    // Notify cursor update after reset
    var centralManager = GetNodeOrNull<CentralInputManager>("/TestBattle2/CentralInputManager");
    if (centralManager != null)
    {
        centralManager.NotifyButtonFocusChanged();
    }
}
    
    #endregion

    #region Grid Helpers

    private BaseButton GetButtonAt(int row, int col)
    {
        int index = row * 2 + col; // Convert 2D to linear index
        return (index >= 0 && index < buttons.Count) ? buttons[index] : null;
    }
    
    private int GetLinearIndex()
    {
        return currentRow * 2 + currentCol;
    }
    
    #endregion
    
    #region Internal Implementation
    
    private void InitializeMenu()
    {
        FindButtonContainer();
        DiscoverButtons();
        
        currentRow = 0;
        currentCol = 0;
        isActive = false;
        
        if (autoActivateOnShow && Visible)
            SetActive(true);
            
        // Debug: print button assignments
        if (buttons.Count == 4)
        {
            GD.Print($"[MenuControls] 2x2 Grid assigned:");
            GD.Print($"  (0,0) = {buttons[0]?.Name}  (0,1) = {buttons[1]?.Name}");
            GD.Print($"  (1,0) = {buttons[2]?.Name}  (1,1) = {buttons[3]?.Name}");
        }
        else
        {
            GD.PrintErr($"[MenuControls] Expected 4 buttons for 2x2 grid, found {buttons.Count}");
        }
    }
    
    private void FindButtonContainer()
    {
        var candidates = new[] { "VBoxContainer", "HBoxContainer", "ButtonContainer", "Buttons", "FlowContainer" };
        
        foreach (var name in candidates)
        {
            buttonContainer = GetNodeOrNull<Container>(name);
            if (buttonContainer != null) return;
        }
        
        buttonContainer = GetChildren().OfType<Container>().FirstOrDefault();
        
        if (buttonContainer == null)
        {
            GD.PrintErr($"[MenuControls] No button container found in {Name}");
        }
    }
    
    private void DiscoverButtons()
    {
        buttons.Clear();
        if (buttonContainer == null) return;
        
        CollectButtons(buttonContainer);
        
        if (buttons.Count == 0)
        {
            GD.PrintErr($"[MenuControls] No buttons found in {Name}");
        }
    }
    
    private void CollectButtons(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            if (child is BaseButton button && button.Visible && !button.Disabled)
            {
                buttons.Add(button);
            }
            else if (child.GetChildCount() > 0)
            {
                CollectButtons(child);
            }
        }
    }
    
    private void ActivateMenu()
    {
        ApplySelection();
        EmitSignal(SignalName.MenuActivated);
    }
    
    private void DeactivateMenu()
    {
        ClearAllButtonFocus();
        EmitSignal(SignalName.MenuDeactivated);
    }
    
    private void ApplySelection()
    {
        ClearAllButtonFocus();
        
        var selectedButton = CurrentButton;
        if (selectedButton != null)
        {
            selectedButton.GrabFocus();
            EmitSignal(SignalName.ButtonSelected, GetLinearIndex(), selectedButton);
        }
    }
    
    private void ClearAllButtonFocus()
    {
        foreach (var button in buttons)
        {
            button?.ReleaseFocus();
        }
    }
    
    #endregion
}