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
    
    [Export] private bool wrapNavigation = true;
    [Export] private bool autoActivateOnShow = true;
    
    private Container buttonContainer;
    private List<BaseButton> buttons = new();
    private int currentIndex = 0;
    private bool isActive = false;
    
    public int CurrentIndex => currentIndex;
    public bool IsActive => isActive;
    public BaseButton CurrentButton => buttons.Count > 0 && IsValidIndex(currentIndex) ? buttons[currentIndex] : null;
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
        if (!isActive || buttons.Count == 0) return;
        
        int newIndex = currentIndex;
        
        if (direction.Y != 0)
            newIndex = CalculateNavigation(direction.Y);
        else if (direction.X != 0)
            newIndex = CalculateNavigation(direction.X);
        
        if (newIndex != currentIndex)
            ApplySelection(newIndex);
    }
    
    public void ActivateCurrentButton()
    {
        if (!IsValidSelection()) return;
        
        var button = buttons[currentIndex];
        button.EmitSignal(BaseButton.SignalName.Pressed);
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
    
    #region Internal Implementation
    
    private void InitializeMenu()
    {
        FindButtonContainer();
        DiscoverButtons();
        
        currentIndex = 0;
        isActive = false;
        
        if (autoActivateOnShow && Visible)
            SetActive(true);
    }
    
    private void FindButtonContainer()
    {
        // Try common container names first
        var candidates = new[] { "VBoxContainer", "HBoxContainer", "ButtonContainer", "Buttons", "FlowContainer" };
        
        foreach (var name in candidates)
        {
            buttonContainer = GetNodeOrNull<Container>(name);
            if (buttonContainer != null) return;
        }
        
        // Fallback to first Container child
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
            GD.PrintErr($"[MenuControls] No buttons found in {Name}");
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
        if (buttons.Count > 0)
        {
            ValidateCurrentSelection();
            ApplySelection(currentIndex);
        }
        
        EmitSignal(SignalName.MenuActivated);
    }
    
    private void DeactivateMenu()
    {
        ClearAllButtonFocus();
        EmitSignal(SignalName.MenuDeactivated);
    }
    
    private int CalculateNavigation(int direction)
    {
        int targetIndex = currentIndex + direction;
        
        if (wrapNavigation)
        {
            if (targetIndex < 0) 
                targetIndex = buttons.Count - 1;
            else if (targetIndex >= buttons.Count) 
                targetIndex = 0;
        }
        else
        {
            targetIndex = Mathf.Clamp(targetIndex, 0, buttons.Count - 1);
        }
        
        return FindNextValidButton(targetIndex, direction);
    }
    
    private int FindNextValidButton(int startIndex, int direction)
    {
        int attempts = 0;
        int index = startIndex;
        
        while (attempts < buttons.Count)
        {
            if (IsValidButtonAtIndex(index))
                return index;
            
            index += direction > 0 ? 1 : -1;
            
            if (wrapNavigation)
            {
                if (index < 0) 
                    index = buttons.Count - 1;
                else if (index >= buttons.Count) 
                    index = 0;
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
    
    private void ClearAllButtonFocus()
    {
        foreach (var button in buttons)
        {
            button?.ReleaseFocus();
        }
    }
    
    private bool IsValidSelection()
    {
        return isActive && IsValidButtonAtIndex(currentIndex);
    }
    
    private bool IsValidIndex(int index)
    {
        return index >= 0 && index < buttons.Count;
    }
    
    private bool IsValidButtonAtIndex(int index)
    {
        return IsValidIndex(index) && buttons[index].Visible && !buttons[index].Disabled;
    }
    
    private void ValidateCurrentSelection()
    {
        if (!IsValidButtonAtIndex(currentIndex))
        {
            // Find first valid button
            for (int i = 0; i < buttons.Count; i++)
            {
                if (IsValidButtonAtIndex(i))
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