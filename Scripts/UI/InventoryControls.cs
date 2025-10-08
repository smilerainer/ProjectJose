// InventoryControls.cs - Inventory screen controller
using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class InventoryControls : Control
{
    #region Exports
    
    [Export] private MenuControls itemListMenu;
    [Export] private MenuControls actionMenu;
    
    [Export] private Label itemNameLabel;
    [Export] private Label itemQuantityLabel;
    [Export] private Label itemDescriptionLabel;
    [Export] private Label itemCategoryLabel;
    
    #endregion
    
    #region State
    
    private InventoryManager.ItemContext currentContext;
    private InventoryManager.InventoryItem selectedItem;
    private CentralInputManager inputManager;
    private bool isActive = false;
    
    #endregion
    
    #region Initialization
    
    public override void _Ready()
    {
        Visible = false;
        
        ValidateExports();
        ConnectSignals();
        
        GD.Print("[InventoryControls] Inventory UI initialized");
    }
    
    private void ValidateExports()
    {
        if (itemListMenu == null)
            GD.PrintErr("[InventoryControls] itemListMenu not assigned in exports!");
        
        if (actionMenu == null)
            GD.PrintErr("[InventoryControls] actionMenu not assigned in exports!");
        
        if (itemNameLabel == null)
            GD.PrintErr("[InventoryControls] itemNameLabel not assigned in exports!");
        
        if (itemQuantityLabel == null)
            GD.PrintErr("[InventoryControls] itemQuantityLabel not assigned in exports!");
        
        if (itemDescriptionLabel == null)
            GD.PrintErr("[InventoryControls] itemDescriptionLabel not assigned in exports!");
        
        if (itemCategoryLabel == null)
            GD.PrintErr("[InventoryControls] itemCategoryLabel not assigned in exports!");
        
        inputManager = GetTree().Root.GetNodeOrNull<CentralInputManager>("CentralInputManager");
    }
    
    private void ConnectSignals()
    {
        if (itemListMenu != null)
        {
            itemListMenu.ButtonSelected += OnItemSelected;
            itemListMenu.ButtonActivated += OnItemActivated;
        }
        
        if (actionMenu != null)
        {
            actionMenu.ButtonActivated += OnActionSelected;
        }
        
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.InventoryChanged += OnInventoryChanged;
        }
    }
    
    #endregion
    
    #region Public API
    
    public void Open(InventoryManager.ItemContext context = InventoryManager.ItemContext.Menu)
    {
        currentContext = context;
        isActive = true;
        Visible = true;
        
        RefreshItemList();
        
        if (itemListMenu != null)
        {
            itemListMenu.SetActive(true);
            itemListMenu.ResetToFirstButton();
        }
        
        GD.Print($"[InventoryUI] Opened in {context} context");
    }
    
    public void Close()
    {
        isActive = false;
        Visible = false;
        
        if (itemListMenu != null)
            itemListMenu.SetActive(false);
        
        if (actionMenu != null)
            actionMenu.SetActive(false);
        
        GD.Print("[InventoryUI] Closed");
    }
    
    #endregion
    
    #region Item List Management
    
    private void RefreshItemList()
    {
        if (itemListMenu == null || InventoryManager.Instance == null)
            return;
        
        itemListMenu.ClearAllButtons();
        
        var items = InventoryManager.Instance.GetItemsByContext(currentContext);
        
        if (items.Count == 0)
        {
            itemListMenu.AddButton("(Empty)", "empty");
            ClearItemDetails();
            return;
        }
        
        foreach (var item in items)
        {
            string buttonText = $"{item.Name} x{item.Quantity}";
            itemListMenu.AddButton(buttonText, item.Id);
        }
        
        itemListMenu.ResetToFirstButton();
        
        // Auto-select first item
        if (items.Count > 0)
        {
            UpdateItemDetails(items[0]);
        }
    }
    
    private void OnInventoryChanged()
    {
        if (isActive)
        {
            RefreshItemList();
        }
    }
    
    #endregion
    
    #region Item Selection
    
    private void OnItemSelected(int index, BaseButton button)
    {
        string itemId = button.Name;
        
        if (itemId == "empty")
        {
            ClearItemDetails();
            return;
        }
        
        var item = InventoryManager.Instance.GetItem(itemId);
        if (item != null)
        {
            UpdateItemDetails(item);
        }
    }
    
    private void OnItemActivated(int index, BaseButton button)
    {
        string itemId = button.Name;
        
        if (itemId == "empty")
            return;
        
        var item = InventoryManager.Instance.GetItem(itemId);
        if (item != null)
        {
            ShowActionMenu(item);
        }
    }
    
    #endregion
    
    #region Item Details Display
    
    private void UpdateItemDetails(InventoryManager.InventoryItem item)
    {
        selectedItem = item;
        
        if (itemNameLabel != null)
            itemNameLabel.Text = item.Name;
        
        if (itemQuantityLabel != null)
            itemQuantityLabel.Text = $"x{item.Quantity}";
        
        if (itemDescriptionLabel != null)
            itemDescriptionLabel.Text = item.Description;
        
        if (itemCategoryLabel != null)
            itemCategoryLabel.Text = $"[{item.Category}]";
    }
    
    private void ClearItemDetails()
    {
        selectedItem = null;
        
        if (itemNameLabel != null)
            itemNameLabel.Text = "---";
        
        if (itemQuantityLabel != null)
            itemQuantityLabel.Text = "";
        
        if (itemDescriptionLabel != null)
            itemDescriptionLabel.Text = "No items available";
        
        if (itemCategoryLabel != null)
            itemCategoryLabel.Text = "";
    }
    
    #endregion
    
    #region Action Menu
    
    private void ShowActionMenu(InventoryManager.InventoryItem item)
    {
        if (actionMenu == null)
            return;
        
        selectedItem = item;
        
        // Deactivate item list
        if (itemListMenu != null)
            itemListMenu.SetActive(false);
        
        // Build context-aware action list
        var actions = new List<string>();
        
        if (InventoryManager.Instance.CanUseItem(item.Id, currentContext))
        {
            actions.Add(GetUseActionText(item));
        }
        
        if (!item.IsKeyItem)
        {
            actions.Add("Discard");
        }
        
        actions.Add("Cancel");
        
        actionMenu.ClearAllButtons();
        actionMenu.SetButtonsFromArray(actions.ToArray());
        actionMenu.SetActive(true);
        actionMenu.ResetToFirstButton();
    }
    
    private string GetUseActionText(InventoryManager.InventoryItem item)
    {
        return currentContext switch
        {
            InventoryManager.ItemContext.Battle => "Use",
            InventoryManager.ItemContext.Dialogue => item.Type == InventoryManager.ItemType.Gift ? "Give" : "Use",
            InventoryManager.ItemContext.Menu => "Use",
            InventoryManager.ItemContext.Overworld => "Use",
            _ => "Use"
        };
    }
    
    private void HideActionMenu()
    {
        if (actionMenu != null)
            actionMenu.SetActive(false);
        
        if (itemListMenu != null)
            itemListMenu.SetActive(true);
    }
    
    #endregion
    
    #region Action Execution
    
    private void OnActionSelected(int index, BaseButton button)
    {
        string action = GetButtonText(button);
        
        switch (action)
        {
            case "Use":
            case "Give":
                UseSelectedItem();
                break;
            
            case "Discard":
                DiscardSelectedItem();
                break;
            
            case "Cancel":
                HideActionMenu();
                break;
        }
    }
    
    private void UseSelectedItem()
    {
        if (selectedItem == null)
        {
            HideActionMenu();
            return;
        }
        
        if (currentContext == InventoryManager.ItemContext.Battle)
        {
            // Switch to battle targeting mode
            NotifyBattleManager();
        }
        else
        {
            // Direct usage
            bool success = InventoryManager.Instance.UseItem(selectedItem.Id, currentContext);
            
            if (success)
            {
                GD.Print($"[InventoryUI] Used {selectedItem.Name}");
            }
            else
            {
                GD.Print($"[InventoryUI] Failed to use {selectedItem.Name}");
            }
        }
        
        HideActionMenu();
        RefreshItemList();
    }
    
    private void DiscardSelectedItem()
    {
        if (selectedItem == null || selectedItem.IsKeyItem)
        {
            HideActionMenu();
            return;
        }
        
        // TODO: Add confirmation dialog
        bool removed = InventoryManager.Instance.RemoveItem(selectedItem.Id, 1);
        
        if (removed)
        {
            GD.Print($"[InventoryUI] Discarded {selectedItem.Name}");
        }
        
        HideActionMenu();
        RefreshItemList();
    }
    
    private void NotifyBattleManager()
    {
        // Signal battle manager to use this item
        var battleManager = GetTree().Root.GetNodeOrNull<BattleManager>("BattleManager");
        if (battleManager != null)
        {
            battleManager.OnSubmenuSelection(selectedItem.Name);
            Close(); // Close inventory, let battle handle targeting
        }
    }
    
    #endregion
    
    #region Input Handling
    
    public override void _Input(InputEvent @event)
    {
        if (!isActive)
            return;
        
        // Handle escape/cancel to close inventory
        if (@event.IsActionPressed("ui_cancel"))
        {
            if (actionMenu != null && actionMenu.IsActive)
            {
                HideActionMenu();
            }
            else
            {
                Close();
            }
            
            GetViewport().SetInputAsHandled();
        }
    }
    
    #endregion
    
    #region Helpers
    
    private string GetButtonText(BaseButton button)
    {
        if (button is Button btn)
            return btn.Text;
        
        return button.Name;
    }
    
    #endregion
}