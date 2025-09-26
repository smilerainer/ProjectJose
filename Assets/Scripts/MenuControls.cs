// MenuControls.cs
using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class MenuControls : Container
{
    [Signal] public delegate void ButtonSelectedEventHandler(int index, BaseButton button);
    [Signal] public delegate void ButtonActivatedEventHandler(int index, BaseButton button);
    [Signal] public delegate void MenuActivatedEventHandler();
    [Signal] public delegate void MenuDeactivatedEventHandler();
    
    [Export] private bool wrapNavigation = true;
    [Export] private bool autoActivateOnShow = true;
    
    private Container buttonContainer;
    private List<BaseButton> buttons = new();
    private int currentRow = 0;
    private int currentCol = 0;
    private bool isActive = false;
    private CentralInputManager inputManager;
    
    public bool IsActive => isActive;
    public BaseButton CurrentButton => GetButtonAt(currentRow, currentCol);
    public Vector2 CurrentButtonPosition => CurrentButton?.GlobalPosition ?? Vector2.Zero;
    
    public override void _Ready()
    {
        InitializeMenu();
        FindInputManager();
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
        
        // Always notify cursor when activation state changes
        NotifyInputManagerCursorUpdate();
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
            
            // ONLY notify cursor when navigation actually succeeds
            NotifyInputManagerCursorUpdate();
        }
        else
        {
            GD.Print($"[MenuControls] Navigate: ({currentRow},{currentCol}) - no movement (blocked or same position)");
            // Do NOT notify cursor when navigation is blocked
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
        NotifyInputManagerCursorUpdate();
    }
    
    // Add a button to this menu's container
    public BaseButton AddButton(string buttonText, string buttonName = "")
    {
        if (buttonContainer == null)
        {
            GD.PrintErr($"[MenuControls] Cannot add button - no container found in {Name}");
            return null;
        }
        
        var button = new Button();
        button.Text = buttonText;
        button.Name = string.IsNullOrEmpty(buttonName) ? buttonText : buttonName;
        
        // Add to container
        buttonContainer.AddChild(button);
        
        // Refresh our button list
        DiscoverButtons();
        
        GD.Print($"[MenuControls] Added button '{button.Name}' with text '{buttonText}' to {Name}");
        GD.Print($"[MenuControls] Total buttons now: {buttons.Count}");
        
        return button;
    }
    
    // Delete a specific button by name
    public bool DeleteButton(string buttonName)
    {
        if (buttonContainer == null)
        {
            GD.PrintErr($"[MenuControls] Cannot delete button - no container found in {Name}");
            return false;
        }
        
        var button = buttons.FirstOrDefault(b => b.Name == buttonName);
        if (button == null)
        {
            GD.PrintErr($"[MenuControls] Button '{buttonName}' not found in {Name}");
            return false;
        }
        
        // Remove from scene and free memory
        button.QueueFree();
        
        // Refresh our button list
        DiscoverButtons();
        
        GD.Print($"[MenuControls] Deleted button '{buttonName}' from {Name}");
        GD.Print($"[MenuControls] Total buttons now: {buttons.Count}");
        
        return true;
    }
    
    // Delete a button by index
    public bool DeleteButtonAt(int index)
    {
        if (index < 0 || index >= buttons.Count)
        {
            GD.PrintErr($"[MenuControls] Button index {index} out of range (0-{buttons.Count - 1}) in {Name}");
            return false;
        }
        
        var button = buttons[index];
        return DeleteButton(button.Name);
    }
    
    // Clear all buttons from the container
    public void ClearAllButtons()
    {
        if (buttonContainer == null)
        {
            GD.PrintErr($"[MenuControls] Cannot clear buttons - no container found in {Name}");
            return;
        }
        
        // Remove all button children immediately, not with QueueFree()
        foreach (var button in buttons.ToList()) // ToList() to avoid modification during iteration
        {
            button.GetParent()?.RemoveChild(button);
            button.QueueFree(); // Still queue for memory cleanup
        }
        
        // Clear our list immediately since nodes are removed from tree
        buttons.Clear();
        
        GD.Print($"[MenuControls] Cleared all buttons from {Name}");
    }
    // Set buttons from a text array - wipes existing and creates new ones
    public void SetButtonsFromArray(string[] buttonTexts)
    {
        if (buttonTexts == null || buttonTexts.Length == 0)
        {
            GD.PrintErr($"[MenuControls] Cannot set buttons - array is null or empty");
            return;
        }
        
        GD.Print($"[MenuControls] Setting {buttonTexts.Length} buttons from array in {Name}");
        
        // Step 1: Clear all existing buttons
        ClearAllButtons();
        
        // Step 2: Add new buttons from array
        for (int i = 0; i < buttonTexts.Length; i++)
        {
            var buttonText = buttonTexts[i];
            var buttonName = $"ArrayButton_{i}";
            AddButton(buttonText, buttonName);
        }
        
        // Reset to first button
        currentRow = 0;
        currentCol = 0;
        ApplySelection();
        
        GD.Print($"[MenuControls] Successfully set {buttons.Count} buttons from array");
    }
    
    #endregion

    #region Cursor Integration
    
    private void FindInputManager()
    {
        // Try multiple common paths for the input manager
        inputManager = GetNodeOrNull<CentralInputManager>("/TestBattle2/CentralInputManager");
        
        if (inputManager == null)
        {
            // Try finding it anywhere in the scene tree
            inputManager = GetTree().GetFirstNodeInGroup("input_manager") as CentralInputManager;
        }
        
        if (inputManager == null)
        {
            // Try finding it as a child of the scene root
            var sceneRoot = GetTree().CurrentScene;
            inputManager = sceneRoot.GetNodeOrNull<CentralInputManager>("CentralInputManager");
        }
        
        if (inputManager == null)
        {
            // Last resort: search recursively
            inputManager = FindInputManagerRecursive(GetTree().CurrentScene);
        }
        
        if (inputManager != null)
        {
            GD.Print($"[MenuControls] Found CentralInputManager for {Name}");
        }
        else
        {
            GD.PrintErr($"[MenuControls] Could not find CentralInputManager for {Name}");
        }
    }
    
    private CentralInputManager FindInputManagerRecursive(Node node)
    {
        if (node is CentralInputManager manager)
            return manager;
            
        foreach (Node child in node.GetChildren())
        {
            var found = FindInputManagerRecursive(child);
            if (found != null)
                return found;
        }
        
        return null;
    }
    
    private void NotifyInputManagerCursorUpdate()
    {
        // Simplified debug output - only show when actually changing buttons
        var currentButton = CurrentButton;
        
        if (inputManager != null)
        {
            inputManager.NotifyButtonFocusChanged();
        }
        else
        {
            // Try to find it again if it wasn't found before
            FindInputManager();
            inputManager?.NotifyButtonFocusChanged();
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
        {
            SetActive(true);
        }
            
        // Debug: print button assignments
        if (buttons.Count == 2)
        {
            GD.Print($"[MenuControls] 2-button layout assigned:");
            GD.Print($"  (0,0) = {buttons[0]?.Name}  (0,1) = {buttons[1]?.Name}");
        }
        else if (buttons.Count == 4)
        {
            GD.Print($"[MenuControls] 2x2 Grid assigned:");
            GD.Print($"  (0,0) = {buttons[0]?.Name}  (0,1) = {buttons[1]?.Name}");
            GD.Print($"  (1,0) = {buttons[2]?.Name}  (1,1) = {buttons[3]?.Name}");
        }
        else
        {
            GD.Print($"[MenuControls] Generic layout with {buttons.Count} buttons:");
            for (int i = 0; i < buttons.Count; i++)
            {
                GD.Print($"  [{i}] = {buttons[i]?.Name}");
            }
        }
    }
    
    private void FindButtonContainer()
    {
        // Since MenuControls now inherits from VBoxContainer, it IS the container
        buttonContainer = this;
        GD.Print($"[MenuControls] Using self as button container: {Name}");
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