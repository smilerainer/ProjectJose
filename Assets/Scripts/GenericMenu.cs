// GenericMenu.cs - Versatile menu controller for visual novel, popups, and game menus
using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class GenericMenu : Control
{
    public enum MenuType
    {
        Linear,     // Standard vertical/horizontal navigation
        Grid,       // 2D grid navigation
        Radial,     // Circular navigation
        VN          // Visual novel style (single choice highlight)
    }
    
    [Signal] public delegate void ButtonSelectedEventHandler(int index, string buttonText);
    [Signal] public delegate void ButtonActivatedEventHandler(int index, string buttonText);
    [Signal] public delegate void MenuOpenedEventHandler();
    [Signal] public delegate void MenuClosedEventHandler();
    
    [Export] private Container buttonContainer;
    [Export] private MenuType menuType = MenuType.Linear;
    [Export] private bool autoFocus = true;
    [Export] private bool wrapNavigation = false;
    [Export] private int gridColumns = 3;
    [Export] private AudioStream selectSound;
    [Export] private AudioStream activateSound;
    
    private List<BaseButton> buttons = new();
    private int currentIndex = 0;
    private bool isActive = false;
    private bool isVisible = false;
    private AudioStreamPlayer audioPlayer;
    private Tween animationTween;
    private Vector2 originalScale;
    
    public int ButtonCount => buttons.Count;
    public int CurrentIndex => currentIndex;
    public bool IsActive => isActive;
    public string CurrentButtonText => buttons.Count > 0 ? GetButtonText(buttons[currentIndex]) : "";
    
    public override void _Ready()
    {
        InitializeComponents();
        CacheButtonReferences();
        SetupInitialState();
    }
    
    // Public API - Simple Commands
    public void Open()
    {
        if (isVisible) return;
        
        ShowMenu();
        if (autoFocus && buttons.Count > 0)
            SelectFirst();
    }
    
    public void Close()
    {
        if (!isVisible) return;
        
        HideMenu();
    }
    
    public void SelectFirst() => NavigateToIndex(0);
    public void SelectLast() => NavigateToIndex(buttons.Count - 1);
    public void SelectNext() => HandleDirectionalInput(new Vector2I(0, 1));
    public void SelectPrevious() => HandleDirectionalInput(new Vector2I(0, -1));
    public void ActivateCurrent() => ExecuteCurrentButton();
    
    public void Navigate(Vector2I direction)
    {
        if (!isActive) return;
        HandleDirectionalInput(direction);
    }
    
    public void SetButtonText(int index, string text)
    {
        if (IsValidIndex(index))
            SetButtonTextInternal(buttons[index], text);
    }
    
    public void SetButtonEnabled(int index, bool enabled)
    {
        if (IsValidIndex(index))
            buttons[index].Disabled = !enabled;
    }
    
    public void RefreshButtons()
    {
        CacheButtonReferences();
        ValidateCurrentSelection();
    }
    
    // Private Methods - Complex Logic
    private void InitializeComponents()
    {
        // Auto-find button container if not set
        buttonContainer ??= FindButtonContainer();
        
        // Setup audio player
        audioPlayer = new AudioStreamPlayer();
        AddChild(audioPlayer);
        
        // Store original scale for animations
        originalScale = Scale;
        
        // Create animation tween
        animationTween = CreateTween();
        animationTween.Kill();
    }
    
    private Container FindButtonContainer()
    {
        // Look for common container node names
        var candidates = new[] { "ButtonContainer", "Buttons", "VBoxContainer", "HBoxContainer", "GridContainer" };
        
        foreach (var name in candidates)
        {
            var container = GetNodeOrNull<Container>(name);
            if (container != null) return container;
        }
        
        // Fallback to first Container child
        foreach (Node child in GetChildren())
        {
            if (child is Container container)
                return container;
        }
        
        GD.PrintErr("[GenericMenu] No button container found!");
        return null;
    }
    
    private void CacheButtonReferences()
    {
        buttons.Clear();
        
        if (buttonContainer == null) return;
        
        CollectButtonsRecursively(buttonContainer);
        EstablishButtonConnections();
        
        GD.Print($"[GenericMenu] Found {buttons.Count} buttons");
    }
    
    private void CollectButtonsRecursively(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            if (child is BaseButton button && button.Visible)
            {
                buttons.Add(button);
            }
            else if (child.GetChildCount() > 0)
            {
                CollectButtonsRecursively(child);
            }
        }
    }
    
    private void EstablishButtonConnections()
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            var button = buttons[i];
            var index = i; // Closure capture
            
            // Connect signals safely
            ConnectButtonPressed(button, index);
            
            // Visual novel style - also connect hover
            if (menuType == MenuType.VN)
            {
                ConnectButtonHover(button, index);
            }
        }
    }
    
    private void SetupInitialState()
    {
        isActive = false;
        isVisible = false;
        Visible = false;
        
        // VN style setup - make all buttons unfocused initially
        if (menuType == MenuType.VN)
        {
            foreach (var button in buttons)
            {
                button.FocusMode = Control.FocusModeEnum.None;
                button.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
            }
        }
    }
    
    private void ShowMenu()
    {
        isVisible = true;
        isActive = true;
        Visible = true;
        
        PlayMenuAnimation(true);
        EmitSignal(SignalName.MenuOpened);
    }
    
    private void HideMenu()
    {
        isActive = false;
        
        PlayMenuAnimation(false, () => {
            isVisible = false;
            Visible = false;
            ClearAllButtonFocus();
            EmitSignal(SignalName.MenuClosed);
        });
    }
    
    private void PlayMenuAnimation(bool opening, System.Action onComplete = null)
    {
        animationTween?.Kill();
        animationTween = CreateTween();
        animationTween.SetParallel(true);
        
        if (opening)
        {
            // Popup animation
            Scale = Vector2.Zero;
            Modulate = new Color(1, 1, 1, 0);
            
            animationTween.TweenProperty(this, "scale", originalScale, 0.2f)
                         .SetEase(Tween.EaseType.Out)
                         .SetTrans(Tween.TransitionType.Back);
                         
            animationTween.TweenProperty(this, "modulate", Colors.White, 0.15f);
        }
        else
        {
            // Close animation
            animationTween.TweenProperty(this, "scale", Vector2.Zero, 0.15f)
                         .SetEase(Tween.EaseType.In)
                         .SetTrans(Tween.TransitionType.Back);
                         
            animationTween.TweenProperty(this, "modulate", new Color(1, 1, 1, 0), 0.15f);
        }
        
        if (onComplete != null)
            animationTween.TweenCallback(Callable.From(onComplete)).SetDelay(0.2f);
    }
    
    private void HandleDirectionalInput(Vector2I direction)
    {
        var newIndex = CalculateNewIndex(direction);
        if (newIndex != currentIndex)
            NavigateToIndex(newIndex);
    }
    
    private int CalculateNewIndex(Vector2I direction)
    {
        if (buttons.Count <= 1) return currentIndex;
        
        return menuType switch
        {
            MenuType.Grid => CalculateGridNavigation(direction),
            MenuType.Radial => CalculateRadialNavigation(direction),
            MenuType.VN => CalculateLinearNavigation(direction.Y),
            _ => CalculateLinearNavigation(direction.Y)
        };
    }
    
    private int CalculateLinearNavigation(int verticalDirection)
    {
        var newIndex = currentIndex + verticalDirection;
        
        if (wrapNavigation)
        {
            if (newIndex < 0) newIndex = buttons.Count - 1;
            else if (newIndex >= buttons.Count) newIndex = 0;
        }
        else
        {
            newIndex = Mathf.Clamp(newIndex, 0, buttons.Count - 1);
        }
        
        return FindNextValidIndex(newIndex, verticalDirection);
    }
    
    private int CalculateGridNavigation(Vector2I direction)
    {
        var rows = (buttons.Count + gridColumns - 1) / gridColumns;
        var currentRow = currentIndex / gridColumns;
        var currentCol = currentIndex % gridColumns;
        
        var newRow = currentRow + direction.Y;
        var newCol = currentCol + direction.X;
        
        // Handle wrapping or clamping
        if (wrapNavigation)
        {
            newRow = ((newRow % rows) + rows) % rows;
            newCol = ((newCol % gridColumns) + gridColumns) % gridColumns;
        }
        else
        {
            newRow = Mathf.Clamp(newRow, 0, rows - 1);
            newCol = Mathf.Clamp(newCol, 0, gridColumns - 1);
        }
        
        var newIndex = newRow * gridColumns + newCol;
        return Mathf.Clamp(newIndex, 0, buttons.Count - 1);
    }
    
    private int CalculateRadialNavigation(Vector2I direction)
    {
        // Simple radial - treat as linear with wrapping
        var movement = direction.X != 0 ? direction.X : direction.Y;
        var newIndex = currentIndex + movement;
        
        if (newIndex < 0) newIndex = buttons.Count - 1;
        else if (newIndex >= buttons.Count) newIndex = 0;
        
        return newIndex;
    }
    
    private int FindNextValidIndex(int startIndex, int direction)
    {
        var index = startIndex;
        var attempts = 0;
        
        while (attempts < buttons.Count)
        {
            if (IsValidIndex(index) && !buttons[index].Disabled)
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
        
        return currentIndex; // No valid button found, stay current
    }
    
    private void NavigateToIndex(int index)
    {
        if (!IsValidIndex(index) || index == currentIndex) return;
        
        ApplyButtonSelection(index);
        PlaySelectionFeedback();
    }
    
    private void ApplyButtonSelection(int index)
    {
        // Clear previous selection
        ClearAllButtonFocus();
        
        currentIndex = index;
        var selectedButton = buttons[currentIndex];
        
        // Apply selection based on menu type
        if (menuType == MenuType.VN)
        {
            HighlightButtonVNStyle(selectedButton);
        }
        else
        {
            selectedButton.GrabFocus();
        }
        
        EmitSignal(SignalName.ButtonSelected, currentIndex, GetButtonText(selectedButton));
    }
    
    private void HighlightButtonVNStyle(BaseButton button)
    {
        // Custom VN highlighting - could be color, scale, etc.
        foreach (var btn in buttons)
        {
            btn.Modulate = btn == button ? new Color(1.2f, 1.2f, 1.0f) : Colors.White;
        }
    }
    
    private void ExecuteCurrentButton()
    {
        if (!IsValidButtonActivation()) return;
        
        var button = buttons[currentIndex];
        PlayActivationFeedback();
        
        // Trigger the button
        TriggerButton(button);
    }
    
    private bool IsValidButtonActivation()
    {
        return isActive && IsValidIndex(currentIndex) && !buttons[currentIndex].Disabled;
    }
    
    private void PlaySelectionFeedback()
    {
        if (selectSound != null && audioPlayer != null)
        {
            audioPlayer.Stream = selectSound;
            audioPlayer.Play();
        }
    }
    
    private void PlayActivationFeedback()
    {
        if (activateSound != null && audioPlayer != null)
        {
            audioPlayer.Stream = activateSound;
            audioPlayer.Play();
        }
    }
    
    private void ClearAllButtonFocus()
    {
        foreach (var button in buttons)
        {
            button.ReleaseFocus();
            button.Modulate = Colors.White; // Reset VN style highlighting
        }
    }
    
    private bool IsValidIndex(int index) => index >= 0 && index < buttons.Count;
    
    private void ValidateCurrentSelection()
    {
        if (!IsValidIndex(currentIndex))
            currentIndex = 0;
    }
    
    // Signal Handlers
    private void OnButtonActivated(int index)
    {
        currentIndex = index;
        EmitSignal(SignalName.ButtonActivated, index, GetButtonText(buttons[index]));
    }
    
    private void OnButtonHovered(int index)
    {
        if (menuType == MenuType.VN)
            NavigateToIndex(index);
    }
    
    // Helper methods for BaseButton compatibility
    private string GetButtonText(BaseButton button)
    {
        return button switch
        {
            Button btn => btn.Text,
            LinkButton linkBtn => linkBtn.Text,
            _ => button.Name
        };
    }
    
    private void SetButtonTextInternal(BaseButton button, string text)
    {
        switch (button)
        {
            case Button btn:
                btn.Text = text;
                break;
            case LinkButton linkBtn:
                linkBtn.Text = text;
                break;
        }
    }
    
    private void ConnectButtonPressed(BaseButton button, int index)
    {
        // Disconnect existing if connected
        if (button.IsConnected(BaseButton.SignalName.Pressed, Callable.From(() => OnButtonActivated(index))))
            button.Disconnect(BaseButton.SignalName.Pressed, Callable.From(() => OnButtonActivated(index)));
        
        // Connect new
        button.Connect(BaseButton.SignalName.Pressed, Callable.From(() => OnButtonActivated(index)));
    }
    
    private void ConnectButtonHover(BaseButton button, int index)
    {
        // Disconnect existing if connected
        var hoverCallable = Callable.From(() => OnButtonHovered(index));
        if (button.IsConnected(Control.SignalName.MouseEntered, hoverCallable))
            button.Disconnect(Control.SignalName.MouseEntered, hoverCallable);
        
        // Connect new
        button.Connect(Control.SignalName.MouseEntered, hoverCallable);
    }
    
    private void TriggerButton(BaseButton button)
    {
        // Manually trigger button press
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }
}
