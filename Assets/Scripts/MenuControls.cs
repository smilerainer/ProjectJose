using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class MenuControls : Container
{
    #region Signals
    
    [Signal] public delegate void ButtonSelectedEventHandler(int index, BaseButton button);
    [Signal] public delegate void ButtonActivatedEventHandler(int index, BaseButton button);
    [Signal] public delegate void MenuActivatedEventHandler();
    [Signal] public delegate void MenuDeactivatedEventHandler();
    
    #endregion
    
    #region Exported Properties
    
    [Export] private bool wrapNavigation = true;
    [Export] private bool autoActivateOnShow = true;
    
    #endregion
    
    #region Private Fields
    
    private Container buttonContainer;
    private List<BaseButton> buttons = new();
    private int currentIndex = 0;
    private bool isActive = false;
    private CentralInputManager inputManager;
    
    #endregion
    
    #region Public Properties
    
    public bool IsActive => isActive;
    public BaseButton CurrentButton => currentIndex >= 0 && currentIndex < buttons.Count ? buttons[currentIndex] : null;
    public Vector2 CurrentButtonPosition => CurrentButton?.GlobalPosition ?? Vector2.Zero;
    
    #endregion
    
    #region Godot Lifecycle
    
    public override void _Ready()
    {
        InitializeMenu();
        FindInputManager();
    }
    
    #endregion
    
    #region Public Core Methods
    
    public void SetActive(bool active)
    {
        if (isActive == active) return;
        isActive = active;
        
        if (active)
            ActivateMenu();
        else
            DeactivateMenu();
        
        NotifyInputManagerCursorUpdate();
    }
    
    public void Navigate(Vector2I direction)
    {
        if (!isActive || buttons.Count == 0) return;
        
        int newIndex = currentIndex;
        
        // Simple: any direction moves to next/previous button
        if (direction.Y > 0 || direction.X > 0) // Down or Right = Next
        {
            newIndex++;
        }
        else if (direction.Y < 0 || direction.X < 0) // Up or Left = Previous  
        {
            newIndex--;
        }
        
        // Handle wrapping
        if (wrapNavigation)
        {
            newIndex = (newIndex + buttons.Count) % buttons.Count;
        }
        else
        {
            newIndex = Mathf.Clamp(newIndex, 0, buttons.Count - 1);
        }
        
        if (newIndex != currentIndex)
        {
            currentIndex = newIndex;
            ApplySelection();
            NotifyInputManagerCursorUpdate();
        }
    }
    
    public void ActivateCurrentButton()
    {
        var button = CurrentButton;
        if (button == null) return;
        
        button.EmitSignal(BaseButton.SignalName.Pressed);
        EmitSignal(SignalName.ButtonActivated, currentIndex, button);
    }
    
    public void ResetToFirstButton()
    {
        currentIndex = 0;
        ApplySelection();
        NotifyInputManagerCursorUpdate();
    }
    
    public bool NavigateToButton(int index)
    {
        if (index < 0 || index >= buttons.Count)
            return false;
        
        currentIndex = index;
        ApplySelection();
        NotifyInputManagerCursorUpdate();
        return true;
    }
    
    #endregion
    
    #region Button Management
    
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
        
        buttonContainer.AddChild(button);
        DiscoverButtons();
        
        GD.Print($"[MenuControls] Added button '{button.Name}' with text '{buttonText}' to {Name}");
        return button;
    }
    
    public bool DeleteButton(string buttonName)
    {
        if (buttonContainer == null)
            return false;
        
        var button = buttons.FirstOrDefault(b => b.Name == buttonName);
        if (button == null)
            return false;
        
        button.QueueFree();
        DiscoverButtons();
        return true;
    }
    
    public bool DeleteButtonAt(int index)
    {
        if (index < 0 || index >= buttons.Count)
            return false;
        
        var button = buttons[index];
        return DeleteButton(button.Name);
    }
    
    public void ClearAllButtons()
    {
        if (buttonContainer == null)
            return;
        
        foreach (var button in buttons.ToList())
        {
            button.GetParent()?.RemoveChild(button);
            button.QueueFree();
        }
        
        buttons.Clear();
    }
    
    public void SetButtonsFromArray(string[] buttonTexts)
    {
        if (buttonTexts == null || buttonTexts.Length == 0)
            return;
        
        ClearAllButtons();
        
        for (int i = 0; i < buttonTexts.Length; i++)
        {
            var buttonText = buttonTexts[i];
            var buttonName = $"ArrayButton_{i}";
            AddButton(buttonText, buttonName);
        }
        
        ResetToFirstButton();
    }
    
    #endregion
    
    #region Button Information
    
    public int GetLinearIndex()
    {
        return currentIndex;
    }
    
    public Vector2I GetCurrentGridPosition()
    {
        return new Vector2I(currentIndex % 2, currentIndex / 2); // Just for compatibility
    }
    
    public int GetButtonCount()
    {
        return buttons.Count;
    }
    
    public string GetCurrentButtonText()
    {
        var button = CurrentButton;
        return GetButtonText(button);
    }
    
    public string[] GetAllButtonTexts()
    {
        var texts = new string[buttons.Count];
        for (int i = 0; i < buttons.Count; i++)
        {
            texts[i] = GetButtonText(buttons[i]);
        }
        return texts;
    }
    
    public Dictionary<string, Variant> GetButtonInfo(int index)
    {
        var info = new Dictionary<string, Variant>();
        
        if (index >= 0 && index < buttons.Count)
        {
            var button = buttons[index];
            info["index"] = index;
            info["name"] = button.Name;
            info["text"] = GetButtonText(button);
            info["position"] = button.GlobalPosition;
            info["size"] = button.Size;
            info["visible"] = button.Visible;
            info["disabled"] = button.Disabled;
        }
        
        return info;
    }
    
    public bool HasButton(string buttonName)
    {
        return buttons.Exists(b => b.Name == buttonName);
    }
    
    public BaseButton GetButtonByName(string buttonName)
    {
        return buttons.FirstOrDefault(b => b.Name == buttonName);
    }
    
    #endregion
    
    #region Button State Management
    
    public void SetButtonEnabled(string buttonName, bool enabled)
    {
        var button = GetButtonByName(buttonName);
        if (button != null)
        {
            button.Disabled = !enabled;
        }
    }
    
    public void ConfigureAsSubmenu(bool wrapNav = true, bool autoActivate = false)
    {
        wrapNavigation = wrapNav;
        autoActivateOnShow = autoActivate;
        currentIndex = 0;
    }
    
    #endregion
    
    #region State Persistence
    
    public Dictionary<string, Variant> SaveState()
    {
        var state = new Dictionary<string, Variant>();
        state["active"] = isActive;
        state["index"] = currentIndex;
        state["button_count"] = buttons.Count;
        state["button_texts"] = GetAllButtonTexts();
        return state;
    }
    
    public void RestoreState(Dictionary<string, Variant> state)
    {
        if (state.ContainsKey("index"))
        {
            currentIndex = state["index"].AsInt32();
        }
        
        if (state.ContainsKey("active"))
        {
            SetActive(state["active"].AsBool());
        }
        
        ApplySelection();
    }
    
    #endregion
    
    #region Private Initialization Methods
    
    private void InitializeMenu()
    {
        FindButtonContainer();
        DiscoverButtons();
        
        currentIndex = 0;
        isActive = false;
        
        if (autoActivateOnShow && Visible)
        {
            SetActive(true);
        }
    }
    
    private void FindButtonContainer()
    {
        buttonContainer = this;
    }
    
    private void DiscoverButtons()
    {
        buttons.Clear();
        if (buttonContainer == null) return;
        
        CollectButtons(buttonContainer);
        
        // Ensure current index is valid
        if (currentIndex >= buttons.Count)
        {
            currentIndex = 0;
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
    
    #endregion
    
    #region Private UI Methods
    
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
            EmitSignal(SignalName.ButtonSelected, currentIndex, selectedButton);
        }
    }
    
    private void ClearAllButtonFocus()
    {
        foreach (var button in buttons)
        {
            button?.ReleaseFocus();
        }
    }
    
    private string GetButtonText(BaseButton button)
    {
        if (button == null) return "";
        
        if (button is Button btn)
            return btn.Text;
        else if (button is LinkButton linkBtn)
            return linkBtn.Text;
        else if (button is OptionButton optBtn)
            return optBtn.Text;
        
        return button.Name;
    }
    
    #endregion
    
    #region Input Manager Integration
    
    private void FindInputManager()
    {
        inputManager = GetNodeOrNull<CentralInputManager>("/TestBattle2/CentralInputManager");
        
        if (inputManager == null)
        {
            inputManager = GetTree().GetFirstNodeInGroup("input_manager") as CentralInputManager;
        }
        
        if (inputManager == null)
        {
            var sceneRoot = GetTree().CurrentScene;
            inputManager = sceneRoot.GetNodeOrNull<CentralInputManager>("CentralInputManager");
        }
        
        if (inputManager == null)
        {
            inputManager = FindInputManagerRecursive(GetTree().CurrentScene);
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
        if (inputManager != null)
        {
            inputManager.NotifyButtonFocusChanged();
        }
        else
        {
            FindInputManager();
            inputManager?.NotifyButtonFocusChanged();
        }
    }
    
    #endregion
    
    #region Debug Methods
    
    public void PrintDebugInfo()
    {
        GD.Print($"[MenuControls] === Debug Info for {Name} ===");
        GD.Print($"  Active: {isActive}");
        GD.Print($"  Button Count: {buttons.Count}");
        GD.Print($"  Current Index: {currentIndex}");
        GD.Print($"  Current Button: {GetCurrentButtonText()}");
        GD.Print($"  Wrap Navigation: {wrapNavigation}");
        
        if (buttons.Count > 0)
        {
            GD.Print($"  Buttons:");
            for (int i = 0; i < buttons.Count; i++)
            {
                var info = GetButtonInfo(i);
                var marker = i == currentIndex ? " <-- CURRENT" : "";
                GD.Print($"    [{i}] {info["text"]} (Name: {info["name"]}){marker}");
            }
        }
        GD.Print($"[MenuControls] === End Debug Info ===");
    }
    
    #endregion
}