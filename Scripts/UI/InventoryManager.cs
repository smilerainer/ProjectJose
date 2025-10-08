// InventoryManager.cs - Global inventory system (Autoload Singleton)
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class InventoryManager : Node
{
    #region Singleton
    
    private static InventoryManager instance;
    public static InventoryManager Instance => instance;
    
    #endregion
    
    #region Signals
    
    [Signal] public delegate void InventoryChangedEventHandler();
    [Signal] public delegate void ItemUsedEventHandler(string itemId, bool success);
    [Signal] public delegate void ItemAddedEventHandler(string itemId, int quantity);
    [Signal] public delegate void ItemRemovedEventHandler(string itemId, int quantity);
    
    #endregion
    
    #region Data Structures
    
    public class InventoryItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ItemType Type { get; set; }
        public ItemCategory Category { get; set; }
        public int Quantity { get; set; }
        public int MaxStack { get; set; } = 99;
        public bool IsConsumable { get; set; }
        public bool IsKeyItem { get; set; }
        
        // Effects
        public int HealAmount { get; set; }
        public int Damage { get; set; }
        public string StatusEffect { get; set; }
        public int StatusDuration { get; set; }
        
        // Usage contexts
        public HashSet<ItemContext> UsableIn { get; set; } = new();
        
        // Battle-specific (if battle item)
        public int Range { get; set; }
        public string TargetType { get; set; }
        public int Cost { get; set; }
        
        public InventoryItem Clone()
        {
            return new InventoryItem
            {
                Id = Id,
                Name = Name,
                Description = Description,
                Type = Type,
                Category = Category,
                Quantity = Quantity,
                MaxStack = MaxStack,
                IsConsumable = IsConsumable,
                IsKeyItem = IsKeyItem,
                HealAmount = HealAmount,
                Damage = Damage,
                StatusEffect = StatusEffect,
                StatusDuration = StatusDuration,
                UsableIn = new HashSet<ItemContext>(UsableIn),
                Range = Range,
                TargetType = TargetType,
                Cost = Cost
            };
        }
    }
    
    public enum ItemType
    {
        Consumable,
        Equipment,
        KeyItem,
        Material,
        Gift
    }
    
    public enum ItemCategory
    {
        Healing,
        Offensive,
        Defensive,
        Support,
        Quest,
        Misc
    }
    
    public enum ItemContext
    {
        Battle,
        Dialogue,
        Overworld,
        Menu
    }
    
    #endregion
    
    #region Inventory State
    
    private Dictionary<string, InventoryItem> inventory = new();
    private int maxInventorySize = 100;
    
    #endregion
    
    #region Initialization
    
    public override void _Ready()
    {
        if (instance != null && instance != this)
        {
            QueueFree();
            return;
        }
        
        instance = this;
        ProcessMode = ProcessModeEnum.Always;
        
        LoadItemDefinitions();
        InitializeDefaultInventory();
        
        GD.Print("[InventoryManager] Global inventory system initialized");
    }
    
    private void LoadItemDefinitions()
    {
        // TODO: Load from JSON/resource file
        // For now, create test items
        DefineTestItems();
    }
    
    private void DefineTestItems()
    {
        // Example items - replace with actual data loading
        var healthPotion = new InventoryItem
        {
            Id = "health_potion",
            Name = "Health Potion",
            Description = "Restores 50 HP",
            Type = ItemType.Consumable,
            Category = ItemCategory.Healing,
            HealAmount = 50,
            IsConsumable = true,
            UsableIn = { ItemContext.Battle, ItemContext.Menu }
        };
        
        var bomb = new InventoryItem
        {
            Id = "bomb",
            Name = "Bomb",
            Description = "Deals 30 damage in an area",
            Type = ItemType.Consumable,
            Category = ItemCategory.Offensive,
            Damage = 30,
            IsConsumable = true,
            UsableIn = { ItemContext.Battle }
        };
        
        var iceCream = new InventoryItem
        {
            Id = "ice_cream",
            Name = "Ice Cream",
            Description = "A delicious treat. Can be given as a gift.",
            Type = ItemType.Gift,
            Category = ItemCategory.Support,
            IsConsumable = true,
            UsableIn = { ItemContext.Dialogue, ItemContext.Menu }
        };
        
        var keyCard = new InventoryItem
        {
            Id = "key_card",
            Name = "Security Key Card",
            Description = "Opens locked doors.",
            Type = ItemType.KeyItem,
            Category = ItemCategory.Quest,
            IsKeyItem = true,
            MaxStack = 1,
            UsableIn = { ItemContext.Overworld }
        };
        
        GD.Print("[InventoryManager] Test item definitions created");
    }
    
    private void InitializeDefaultInventory()
    {
        // Starting items
        AddItem("health_potion", 3);
        AddItem("ice_cream", 1);
        AddItem("bomb", 2);
    }
    
    #endregion
    
    #region Item Management
    
    public bool AddItem(string itemId, int quantity = 1)
    {
        if (string.IsNullOrEmpty(itemId) || quantity <= 0)
            return false;
        
        if (inventory.ContainsKey(itemId))
        {
            var item = inventory[itemId];
            
            if (item.Quantity + quantity > item.MaxStack)
            {
                GD.PrintErr($"[InventoryManager] Cannot add {quantity}x {itemId} - would exceed max stack");
                return false;
            }
            
            item.Quantity += quantity;
        }
        else
        {
            // Create new item from definition
            var itemDef = GetItemDefinition(itemId);
            if (itemDef == null)
            {
                GD.PrintErr($"[InventoryManager] Item definition not found: {itemId}");
                return false;
            }
            
            var newItem = itemDef.Clone();
            newItem.Quantity = quantity;
            inventory[itemId] = newItem;
        }
        
        EmitSignal(SignalName.ItemAdded, itemId, quantity);
        EmitSignal(SignalName.InventoryChanged);
        
        GD.Print($"[InventoryManager] Added {quantity}x {itemId}");
        return true;
    }
    
    public bool RemoveItem(string itemId, int quantity = 1)
    {
        if (!inventory.ContainsKey(itemId))
            return false;
        
        var item = inventory[itemId];
        
        if (item.Quantity < quantity)
        {
            GD.PrintErr($"[InventoryManager] Not enough {itemId} to remove {quantity}");
            return false;
        }
        
        if (item.IsKeyItem)
        {
            GD.PrintErr($"[InventoryManager] Cannot remove key item: {itemId}");
            return false;
        }
        
        item.Quantity -= quantity;
        
        if (item.Quantity <= 0)
        {
            inventory.Remove(itemId);
        }
        
        EmitSignal(SignalName.ItemRemoved, itemId, quantity);
        EmitSignal(SignalName.InventoryChanged);
        
        GD.Print($"[InventoryManager] Removed {quantity}x {itemId}");
        return true;
    }
    
    public bool HasItem(string itemId, int quantity = 1)
    {
        if (!inventory.ContainsKey(itemId))
            return false;
        
        return inventory[itemId].Quantity >= quantity;
    }
    
    public int GetItemQuantity(string itemId)
    {
        return inventory.ContainsKey(itemId) ? inventory[itemId].Quantity : 0;
    }
    
    #endregion
    
    #region Item Usage
    
    public bool CanUseItem(string itemId, ItemContext context)
    {
        if (!inventory.ContainsKey(itemId))
            return false;
        
        var item = inventory[itemId];
        
        if (item.Quantity <= 0)
            return false;
        
        if (!item.UsableIn.Contains(context))
            return false;
        
        return true;
    }
    
    public bool UseItem(string itemId, ItemContext context, object target = null)
    {
        if (!CanUseItem(itemId, context))
        {
            EmitSignal(SignalName.ItemUsed, itemId, false);
            return false;
        }
        
        var item = inventory[itemId];
        
        // Execute item effect based on context
        bool success = ExecuteItemEffect(item, context, target);
        
        if (success && item.IsConsumable)
        {
            RemoveItem(itemId, 1);
        }
        
        EmitSignal(SignalName.ItemUsed, itemId, success);
        
        return success;
    }
    
    private bool ExecuteItemEffect(InventoryItem item, ItemContext context, object target)
    {
        GD.Print($"[InventoryManager] Using {item.Name} in {context} context");
        
        switch (context)
        {
            case ItemContext.Battle:
                return ExecuteBattleEffect(item, target);
            
            case ItemContext.Dialogue:
                return ExecuteDialogueEffect(item, target);
            
            case ItemContext.Menu:
                return ExecuteMenuEffect(item, target);
            
            case ItemContext.Overworld:
                return ExecuteOverworldEffect(item, target);
            
            default:
                return false;
        }
    }
    
    private bool ExecuteBattleEffect(InventoryItem item, object target)
    {
        // Battle item usage will be handled by BattleActionHandler
        GD.Print($"[InventoryManager] Battle effect for {item.Name}");
        return true;
    }
    
    private bool ExecuteDialogueEffect(InventoryItem item, object target)
    {
        // Dialogue items (gifts, etc.) - could trigger Dialogic events
        GD.Print($"[InventoryManager] Dialogue effect for {item.Name}");
        
        var dialogic = GetNodeOrNull("/root/Dialogic");
        if (dialogic != null)
        {
            // Signal Dialogic that an item was given
            dialogic.Call("set_variable", "last_gift_given", item.Id);
        }
        
        return true;
    }
    
    private bool ExecuteMenuEffect(InventoryItem item, object target)
    {
        // Menu usage (healing outside battle, etc.)
        if (item.HealAmount > 0)
        {
            GD.Print($"[InventoryManager] Healed for {item.HealAmount} HP");
            // TODO: Apply to party member
            return true;
        }
        
        return false;
    }
    
    private bool ExecuteOverworldEffect(InventoryItem item, object target)
    {
        // Key items, puzzle items, etc.
        GD.Print($"[InventoryManager] Overworld effect for {item.Name}");
        return true;
    }
    
    #endregion
    
    #region Queries
    
    public List<InventoryItem> GetAllItems()
    {
        return inventory.Values.OrderBy(i => i.Category).ThenBy(i => i.Name).ToList();
    }
    
    public List<InventoryItem> GetItemsByContext(ItemContext context)
    {
        return inventory.Values
            .Where(i => i.UsableIn.Contains(context))
            .OrderBy(i => i.Category)
            .ThenBy(i => i.Name)
            .ToList();
    }
    
    public List<InventoryItem> GetItemsByCategory(ItemCategory category)
    {
        return inventory.Values
            .Where(i => i.Category == category)
            .OrderBy(i => i.Name)
            .ToList();
    }
    
    public InventoryItem GetItem(string itemId)
    {
        return inventory.GetValueOrDefault(itemId);
    }
    
    public int GetTotalItemCount()
    {
        return inventory.Values.Sum(i => i.Quantity);
    }
    
    public bool IsInventoryFull()
    {
        return inventory.Count >= maxInventorySize;
    }
    
    #endregion
    
    #region Item Definitions (TODO: Replace with file loading)
    
    private InventoryItem GetItemDefinition(string itemId)
    {
        // Temporary - hardcoded definitions
        // TODO: Load from JSON/Resource files
        
        return itemId switch
        {
            "health_potion" => new InventoryItem
            {
                Id = "health_potion",
                Name = "Health Potion",
                Description = "Restores 50 HP",
                Type = ItemType.Consumable,
                Category = ItemCategory.Healing,
                HealAmount = 50,
                IsConsumable = true,
                UsableIn = { ItemContext.Battle, ItemContext.Menu }
            },
            "bomb" => new InventoryItem
            {
                Id = "bomb",
                Name = "Bomb",
                Description = "Deals 30 damage in an area",
                Type = ItemType.Consumable,
                Category = ItemCategory.Offensive,
                Damage = 30,
                IsConsumable = true,
                UsableIn = { ItemContext.Battle }
            },
            "ice_cream" => new InventoryItem
            {
                Id = "ice_cream",
                Name = "Ice Cream",
                Description = "A delicious treat. Can be given as a gift.",
                Type = ItemType.Gift,
                Category = ItemCategory.Support,
                IsConsumable = true,
                UsableIn = { ItemContext.Dialogue, ItemContext.Menu }
            },
            _ => null
        };
    }
    
    #endregion
    
    #region Save/Load (TODO)
    
    public Dictionary<string, Variant> SaveInventory()
    {
        var saveData = new Dictionary<string, Variant>();
        
        foreach (var kvp in inventory)
        {
            saveData[kvp.Key] = kvp.Value.Quantity;
        }
        
        return saveData;
    }
    
    public void LoadInventory(Dictionary<string, Variant> saveData)
    {
        inventory.Clear();
        
        foreach (var kvp in saveData)
        {
            AddItem(kvp.Key, kvp.Value.AsInt32());
        }
        
        EmitSignal(SignalName.InventoryChanged);
    }
    
    #endregion
    
    #region Debug
    
    public void PrintInventory()
    {
        GD.Print("=== INVENTORY ===");
        foreach (var item in GetAllItems())
        {
            GD.Print($"  {item.Name} x{item.Quantity} ({item.Category})");
        }
        GD.Print($"Total items: {GetTotalItemCount()}/{maxInventorySize}");
        GD.Print("=================");
    }
    
    #endregion
}