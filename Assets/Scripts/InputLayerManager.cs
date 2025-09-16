// InputLayerManager.cs - Manages which UI layer has input priority
using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class InputLayerManager : Node
{
    public enum InputLayer
    {
        None = 0,
        HexGrid = 1,      // World navigation
        BattleUI = 2,     // Main battle interface
        PopupMenu = 3,    // Character options popup
        Dialogue = 4,     // Story dialogue (highest priority)
        SystemMenu = 5    // Pause/settings (highest priority)
    }
    
    [Signal] public delegate void LayerChangedEventHandler(InputLayer oldLayer, InputLayer newLayer);
    
    private InputLayer currentLayer = InputLayer.HexGrid;
    private Stack<InputLayer> layerStack = new Stack<InputLayer>();
    private Dictionary<InputLayer, IInputHandler> handlers = new Dictionary<InputLayer, IInputHandler>();
    
    // Singleton pattern for easy access
    public static InputLayerManager Instance { get; private set; }
    
    public InputLayer CurrentLayer => currentLayer;
    public bool IsTopLayer(InputLayer layer) => currentLayer == layer;
    
    public override void _Ready()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            QueueFree();
            return;
        }
        
        // Ensure we process input first
        ProcessPriority = -1000;
        
        GD.Print($"[InputLayerManager] Ready - Current layer: {currentLayer}");
    }
    
    public override void _Input(InputEvent @event)
    {
        // Always handle system-level inputs (like pause menu)
        if (@event.IsActionPressed("ui_cancel"))
        {
            // Only push system menu if we're not already there and not on hex grid
            if (currentLayer != InputLayer.SystemMenu && currentLayer != InputLayer.HexGrid)
            {
                PushLayer(InputLayer.SystemMenu);
                GetViewport().SetInputAsHandled();
                return;
            }
        }
        
        // Route input to current layer handler
        if (handlers.ContainsKey(currentLayer))
        {
            bool handled = handlers[currentLayer].HandleInput(@event);
            if (handled)
            {
                GetViewport().SetInputAsHandled();
            }
        }
    }
    
    public void RegisterHandler(InputLayer layer, IInputHandler handler)
    {
        handlers[layer] = handler;
        GD.Print($"[InputLayerManager] Registered handler for layer: {layer}");
        
        // Notify handler of current state
        handler.SetActive(layer == currentLayer);
    }
    
    public void UnregisterHandler(InputLayer layer)
    {
        if (handlers.ContainsKey(layer))
        {
            handlers[layer].SetActive(false);
            handlers.Remove(layer);
            GD.Print($"[InputLayerManager] Unregistered handler for layer: {layer}");
        }
    }
    
    public void SetLayer(InputLayer newLayer)
    {
        if (newLayer == currentLayer) return;
        
        var oldLayer = currentLayer;
        
        // Deactivate current handler
        if (handlers.ContainsKey(oldLayer))
        {
            handlers[oldLayer].SetActive(false);
        }
        
        // Clear the stack and set new layer
        layerStack.Clear();
        currentLayer = newLayer;
        
        // Activate new handler
        if (handlers.ContainsKey(newLayer))
        {
            handlers[newLayer].SetActive(true);
        }
        
        EmitSignal(SignalName.LayerChanged, (int)oldLayer, (int)newLayer);
        GD.Print($"[InputLayerManager] Layer changed: {oldLayer} -> {newLayer}");
    }
    
    public void PushLayer(InputLayer newLayer)
    {
        if (newLayer == currentLayer) return;
        
        var oldLayer = currentLayer;
        
        // Deactivate current handler
        if (handlers.ContainsKey(oldLayer))
        {
            handlers[oldLayer].SetActive(false);
        }
        
        // Push current layer to stack
        layerStack.Push(currentLayer);
        currentLayer = newLayer;
        
        // Activate new handler
        if (handlers.ContainsKey(newLayer))
        {
            handlers[newLayer].SetActive(true);
        }
        
        EmitSignal(SignalName.LayerChanged, (int)oldLayer, (int)newLayer);
        GD.Print($"[InputLayerManager] Layer pushed: {oldLayer} -> {newLayer} (Stack depth: {layerStack.Count})");
    }
    
    public bool PopLayer()
    {
        if (layerStack.Count == 0)
        {
            GD.Print("[InputLayerManager] Cannot pop - stack is empty");
            return false;
        }
        
        var oldLayer = currentLayer;
        
        // Deactivate current handler
        if (handlers.ContainsKey(oldLayer))
        {
            handlers[oldLayer].SetActive(false);
        }
        
        // Pop previous layer
        currentLayer = layerStack.Pop();
        
        // Activate previous handler
        if (handlers.ContainsKey(currentLayer))
        {
            handlers[currentLayer].SetActive(true);
        }
        
        EmitSignal(SignalName.LayerChanged, (int)oldLayer, (int)currentLayer);
        GD.Print($"[InputLayerManager] Layer popped: {oldLayer} -> {currentLayer} (Stack depth: {layerStack.Count})");
        return true;
    }
    
    public void ClearToLayer(InputLayer targetLayer)
    {
        while (layerStack.Count > 0 && currentLayer != targetLayer)
        {
            PopLayer();
        }
        
        if (currentLayer != targetLayer)
        {
            SetLayer(targetLayer);
        }
    }
    
    // Helper methods for common layer transitions
    public void ShowPopupMenu()
    {
        PushLayer(InputLayer.PopupMenu);
    }
    
    public void HidePopupMenu()
    {
        if (currentLayer == InputLayer.PopupMenu)
        {
            PopLayer();
        }
    }
    
    public void ReturnToHexGrid()
    {
        ClearToLayer(InputLayer.HexGrid);
    }
    
    public void ShowDialogue()
    {
        PushLayer(InputLayer.Dialogue);
    }
    
    public void HideDialogue()
    {
        if (currentLayer == InputLayer.Dialogue)
        {
            PopLayer();
        }
    }
    
    // Debug method
    public void PrintLayerStack()
    {
        GD.Print($"[InputLayerManager] Current: {currentLayer}");
        GD.Print($"[InputLayerManager] Stack: [{string.Join(", ", layerStack.Reverse())}]");
        GD.Print($"[InputLayerManager] Handlers: [{string.Join(", ", handlers.Keys)}]");
    }
}

// Interface that all input handlers must implement
public interface IInputHandler
{
    bool HandleInput(InputEvent inputEvent);
    void SetActive(bool active);
}