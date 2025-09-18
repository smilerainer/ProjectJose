// MenuControls.cs - Generic menu navigation for FlowContainer with BaseButtons
using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class MenuControls : Control
{
    [Signal] public delegate void ButtonSelectedEventHandler(int index, BaseButton button);
    [Signal] public delegate void ButtonActivatedEventHandler(int index, BaseButton button);
    [Signal] public delegate void MenuActivatedEventHandler();
    [Signal] public delegate void MenuDeactivatedEventHandler();
    
    [Export] private Container buttonContainer;
    [Export] private bool wrapNavigation = true;
    [Export] private bool autoActivateOnShow = true;
    
    private List<BaseButton> buttons = new();
    private int currentIndex = 0;
    private bool isActive = false;
    
    public int CurrentIndex => currentIndex;
    public bool IsActive => isActive;
    public BaseButton CurrentButton => buttons.Count > 0 ? buttons[currentIndex] : null;
    public Vector2 CurrentButtonPosition => CurrentButton?.GlobalPosition ?? Vector2.Zero;
    
    #region Public API
    
    public override void _Ready()
    {
        InitializeMenu();
    }
    
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
        if (!isActive || buttons.Count == 0) return;
        
        if (direction.Y != 0)
            NavigateVertically(direction.Y);
        else if (direction.X != 0)
            NavigateHorizontally(direction.X);
    }
    
    public void ActivateCurrentButton()
    {
        if (!isActive || !IsValidSelection()) return;
        
        var button = buttons[currentIndex];
        TriggerButton(button);
        EmitSignal(SignalName.ButtonActivated, currentIndex, button);
    }
    
    public void SelectButton(int index)
    {
        if (!isActive || !IsValidIndex(index)) return;
        
        ApplySelection(index);
    }
    
    public void RefreshButtons()
    {
        DiscoverButtons();
        ValidateCurrentSelection();
    }
    
    #endregion
    
    #region Menu Management
    
    private void InitializeMenu()
    {
        FindButtonContainer();
        DiscoverButtons();
        SetupInitialState();
    }
    
    private void FindButtonContainer()
    {
        if (buttonContainer != null) return;
        
        // Try common container names
        var candidates = new[] { "ButtonContainer", "Buttons", "VBoxContainer", "HBoxContainer", "FlowContainer" };
        
        foreach (var name in candidates)
        {
            buttonContainer = GetNodeOrNull<Container>(name);
            if (buttonContainer != null) return;
        }
        
        // Fallback to first Container child
        buttonContainer = GetChildren().OfType<Container>().FirstOrDefault();
        
        if (buttonContainer == null)
            GD.PrintErr($"[MenuControls] No button container found in {Name}");
    }
    
    private void DiscoverButtons()
    {
        buttons.Clear();
        
        if (buttonContainer == null) return;
        
        CollectButtonsRecursively(buttonContainer);
        
        GD.Print($"[MenuControls] Found {buttons.Count} buttons in {Name}");
    }
    
    private void CollectButtonsRecursively(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            if (child is BaseButton button && button.Visible && !button.Disabled)
            {
                buttons.Add(button);
            }
            else if (child.GetChildCount() > 0)
            {
                CollectButtonsRecursively(child);
            }
        }
    }
    
    private void SetupInitialState()
    {
        currentIndex = 0;
        isActive = false;
        
        if (autoActivateOnShow && Visible)
            SetActive(true);
    }
    
    private void ActivateMenu()
    {
        if (buttons.Count > 0)
        {
            ApplySelection(0);
        }
        
        EmitSignal(SignalName.MenuActivated);
        GD.Print($"[MenuControls] Activated menu: {Name}");
    }
    
    private void DeactivateMenu()
    {
        ClearAllButtonFocus();
        EmitSignal(SignalName.MenuDeactivated);
        GD.Print($"[MenuControls] Deactivated menu: {Name}");
    }
    
    #endregion
    
    #region Navigation Logic
    
    private void NavigateVertically(int direction)
    {
        var newIndex = CalculateVerticalNavigation(direction);
        if (newIndex != currentIndex)
            ApplySelection(newIndex);
    }
    
    private void NavigateHorizontally(int direction)
    {
        var newIndex = CalculateHorizontalNavigation(direction);
        if (newIndex != currentIndex)
            ApplySelection(newIndex);
    }
    
    private int CalculateVerticalNavigation(int direction)
    {
        var targetIndex = currentIndex + direction;
        
        if (wrapNavigation)
        {
            if (targetIndex < 0) targetIndex = buttons.Count - 1;
            else if (targetIndex >= buttons.Count) targetIndex = 0;
        }
        else
        {
            targetIndex = Mathf.Clamp(targetIndex, 0, buttons.Count - 1);
        }
        
        return FindNextValidButton(targetIndex, direction);
    }
    
    private int CalculateHorizontalNavigation(int direction)
    {
        // For now, treat horizontal as vertical navigation
        // Could be extended for grid-like layouts
        return CalculateVerticalNavigation(direction);
    }
    
    private int FindNextValidButton(int startIndex, int direction)
    {
        var attempts = 0;
        var index = startIndex;
        
        while (attempts < buttons.Count)
        {
            if (IsValidIndex(index) && buttons[index].Visible && !buttons[index].Disabled)
                return index;
            
            index += direction > 0 ? 1 : -1;
            
            if (wrapNavigation)
            {
                if (index < 0) index = buttons.Count - 1;
                else if (index >= buttons.Count) index = 0;
            }
            else
            {
                index = Mathf.Clamp(index, 0, buttons.Count - 1);
            }
            
            attempts++;
        }
        
        return currentIndex; // No valid button found
    }
    
    private void ApplySelection(int index)
    {
        if (!IsValidIndex(index)) return;
        
        ClearAllButtonFocus();
        currentIndex = index;
        
        var selectedButton = buttons[currentIndex];
        selectedButton.GrabFocus();
        
        EmitSignal(SignalName.ButtonSelected, currentIndex, selectedButton);
    }
    
    #endregion
    
    #region Button Operations
    
    private void TriggerButton(BaseButton button)
    {
        // Simulate button press
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }
    
    private void ClearAllButtonFocus()
    {
        foreach (var button in buttons)
        {
            button.ReleaseFocus();
        }
    }
    
    private bool IsValidSelection()
    {
        return IsValidIndex(currentIndex) && 
               buttons[currentIndex].Visible && 
               !buttons[currentIndex].Disabled;
    }
    
    private bool IsValidIndex(int index)
    {
        return index >= 0 && index < buttons.Count;
    }
    
    private void ValidateCurrentSelection()
    {
        if (!IsValidIndex(currentIndex) || 
            !buttons[currentIndex].Visible || 
            buttons[currentIndex].Disabled)
        {
            // Find first valid button
            for (int i = 0; i < buttons.Count; i++)
            {
                if (buttons[i].Visible && !buttons[i].Disabled)
                {
                    currentIndex = i;
                    return;
                }
            }
            currentIndex = 0; // Fallback
        }
    }
    
    #endregion
}