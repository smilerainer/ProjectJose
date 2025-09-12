using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public partial class Menu : Control
{
	[Signal] public delegate void ButtonFocusedEventHandler(BaseButton button, int index);
	[Signal] public delegate void ButtonPressedEventHandler(BaseButton button, int index);
	[Signal] public delegate void ActivatedEventHandler();
	[Signal] public delegate void ClosedEventHandler();

	[Export] public bool AutoWrap { get; set; } = true;
	[Export] public Control ButtonsContainer { get; set; } = null;
	[Export] public bool FocusOnReady { get; set; } = false;
	[Export] public bool HideOnStart { get; set; } = false;
	[Export] public bool HideOnFocusExit { get; set; } = false;
	[Export] public bool HideOnClose { get; set; } = false;
	[Export] public bool IsDummy { get; set; } = false;
	[Export] public AudioStream FocusedSound { get; set; }
	[Export] public AudioStream PressedSound { get; set; }

	private static List<Menu> tree = new List<Menu>();

	public int index { get; private set; } = 0;
	private bool exiting = false;
	private bool highlightAll = false;
	private List<BaseButton> buttons = new List<BaseButton>();
	private AudioStreamPlayer streamPlayer;

	// Debug helpers
	private void DebugLog(string message)
	{
		GD.Print($"[Menu:{Name}] {message}");
	}

	public override void _Ready()
	{
		DebugLog("_Ready() called");
		
		SetProcessUnhandledInput(false);
		
		// Initialize audio player
		streamPlayer = new AudioStreamPlayer();
		AddChild(streamPlayer);
		DebugLog("AudioStreamPlayer created");

		if (!IsDummy)
		{
			if (ButtonsContainer == null)
			{
				// Try to find a Control node in children if not explicitly set
				ButtonsContainer = FindButtonsContainer();
				
				if (ButtonsContainer == null)
				{
					GD.PrintErr($"[Menu:{Name}] ERROR: ButtonsContainer not found!");
					return;
				}
			}
			
			// Initialize buttons list from container
			InitializeButtons();
		}
		
		// Connect signals
		Activated += OnActivated;
		Closed += OnClosed;
		TreeExiting += OnTreeExiting;
		
		// Handle initial visibility
		if (HideOnStart)
		{
			Hide();
			DebugLog("Hidden on start");
		}
		
		// Focus first button if needed
		if (FocusOnReady && !HideOnStart)
		{
			CallDeferred(nameof(ButtonFocus));
			DebugLog("Will focus on ready");
		}
	}

	private Control FindButtonsContainer()
	{
		// First, look for a direct Control child
		foreach (Node child in GetChildren())
		{
			if (child is Control control && HasButtonChildren(control))
			{
				DebugLog($"Found buttons container: {control.Name}");
				return control;
			}
		}
		
		// If not found, look deeper in the tree
		return FindButtonsContainerRecursive(this);
	}

	private Control FindButtonsContainerRecursive(Node node)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is Control control && HasButtonChildren(control))
			{
				DebugLog($"Found buttons container recursively: {control.Name}");
				return control;
			}
			
			// Continue searching deeper
			var result = FindButtonsContainerRecursive(child);
			if (result != null)
				return result;
		}
		return null;
	}

	private bool HasButtonChildren(Control control)
	{
		foreach (Node child in control.GetChildren())
		{
			if (child is BaseButton)
				return true;
		}
		return false;
	}

	private void InitializeButtons()
	{
		buttons.Clear();
		
		if (ButtonsContainer == null)
		{
			DebugLog("ButtonsContainer is null, skipping button initialization");
			return;
		}
		
		// Log container type for debugging
		DebugLog($"ButtonsContainer type: {ButtonsContainer.GetClass()}");
		
		int buttonIndex = 0;
		foreach (Node child in ButtonsContainer.GetChildren())
		{
			if (child is BaseButton button)
			{
				buttons.Add(button);
				
				// Connect button signals with proper index capture
				int capturedIndex = buttonIndex;
				BaseButton capturedButton = button;
				
				button.FocusEntered += () => OnButtonFocused(capturedButton, capturedIndex);
				button.FocusExited += () => OnButtonFocusExited(capturedButton);
				button.Pressed += () => OnButtonPressed(capturedButton, capturedIndex);
				button.TreeExiting += OnButtonTreeExiting;
				
				// For FlowContainer/GridContainer, ensure proper focus neighbors are set
				if (ButtonsContainer is FlowContainer || ButtonsContainer is GridContainer)
				{
					// Let Godot handle the default focus neighbors for grid layouts
					// Or optionally set custom neighbors here if needed
				}
				
				DebugLog($"Initialized button {buttonIndex}: {button.Name}");
				buttonIndex++;
			}
			else
			{
				DebugLog($"Child {child.Name} is not a BaseButton, skipping");
			}
		}
		
		DebugLog($"Total buttons initialized: {buttons.Count}");
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!IsProcessingUnhandledInput()) return;
		
		// Add navigation wrapping if AutoWrap is enabled
		if (AutoWrap && buttons.Count > 0)
		{
			if (@event.IsActionPressed("ui_down"))
			{
				var focused = GetViewport().GuiGetFocusOwner() as BaseButton;
				if (focused == buttons.Last() && buttons.First().Visible && !buttons.First().Disabled)
				{
					buttons.First().GrabFocus();
					GetViewport().SetInputAsHandled();
				}
			}
			else if (@event.IsActionPressed("ui_up"))
			{
				var focused = GetViewport().GuiGetFocusOwner() as BaseButton;
				if (focused == buttons.First() && buttons.Last().Visible && !buttons.Last().Disabled)
				{
					buttons.Last().GrabFocus();
					GetViewport().SetInputAsHandled();
				}
			}
		}
	}

	public int GetButtonsCount()
	{
		return buttons.Count;
	}
	
	public List<BaseButton> GetButtons() 
	{ 
		return buttons; 
	}
	
	public bool InMenuTree() 
	{ 
		return tree.Contains(this); 
	}

	public void SetHighlightAll(bool on) 
	{ 
		highlightAll = on;
		DebugLog($"HighlightAll set to: {on}");
	}

	public void ButtonEnableFocus(bool on)
	{
		DebugLog($"ButtonEnableFocus({on})");
		
		Control.FocusModeEnum mode = on ? Control.FocusModeEnum.All : Control.FocusModeEnum.None;
		foreach (BaseButton button in buttons) 
		{ 
			button.FocusMode = mode; 
		}
		
		if (HideOnFocusExit) 
		{ 
			Visible = on; 
		}

		SetProcessUnhandledInput(on);
		// TODO: Globals.MenuHasFocus = on;
	}
	
	public BaseButton GetFirstFocusableButton(bool andFocus = false)
	{
		foreach (BaseButton button in buttons)
		{
			if (button.Visible && !button.Disabled)
			{
				if (andFocus)
				{
					button.GrabFocus();
					DebugLog($"Focused first focusable button: {button.Name}");
				}
				return button;
			}
		}
		DebugLog("No focusable button found");
		return null;
	}
	
	public bool MenuIsFocused()
	{
		Control focusOwner = GetViewport().GuiGetFocusOwner();
		bool isFocused = buttons.Contains(focusOwner as BaseButton);
		DebugLog($"MenuIsFocused: {isFocused}");
		return isFocused;
	}
	
	private void OnTreeExiting()
	{
		DebugLog("Tree exiting");
		exiting = true;
	}

	public void OnButtonFocused(BaseButton button, int buttonIndex)
	{
		DebugLog($"Button focused: {button.Name} at index {buttonIndex}");
		
		index = buttonIndex;
		EmitSignal(SignalName.ButtonFocused, button, index);

		if (FocusedSound != null && streamPlayer != null)
		{
			streamPlayer.Stream = FocusedSound;
			streamPlayer.Play();
		}

		if (highlightAll)
		{
			// TODO: Implement highlight logic for all buttons
			foreach (BaseButton b in buttons)
			{
				// b.Modulate = new Color(1.2f, 1.2f, 1.2f); // Example highlight
			}
		}
	}
	
	public void OnButtonFocusExited(BaseButton button)
	{
		DebugLog($"Button focus exited: {button.Name}");
		
		// Skip if we're exiting the tree
		if (exiting) return;
		
		// Check if focus is still within our menu
		CallDeferred(nameof(CheckFocusExit));
	}
	
	private void CheckFocusExit()
	{
		if (!buttons.Contains(GetViewport().GuiGetFocusOwner() as BaseButton))
		{
			DebugLog("Focus left menu entirely");
			ButtonEnableFocus(false);
			
			if (highlightAll)
			{
				foreach (BaseButton button in buttons)
				{
					// button.Modulate = Colors.White; // Reset highlight
				}
			}
			SetHighlightAll(false);
		}
	}
	
	public void OnButtonPressed(BaseButton button, int buttonIndex)
	{
		DebugLog($"Button pressed: {button.Name} at index {buttonIndex}");
		
		if (PressedSound != null && streamPlayer != null)
		{
			streamPlayer.Stream = PressedSound;
			streamPlayer.Play();
		}
		EmitSignal(SignalName.ButtonPressed, button, buttonIndex);
	}
	
	public void OnButtonTreeExiting() 
	{ 
		exiting = true; 
	}

	public void ButtonFocus(int n = -1)
	{
		if (n == -1) n = index;
		
		DebugLog($"ButtonFocus({n}) called");
		
		if (!MenuIsFocused())
		{
			EmitSignal(SignalName.Activated);
		}

		Show();
		ButtonEnableFocus(true);

		if (buttons.Count > 0)
		{
			n = Mathf.Clamp(n, 0, buttons.Count - 1);
			BaseButton button = buttons[n];
			
			if (button.IsInsideTree())
			{
				if (button.Visible && !button.Disabled) 
				{ 
					button.GrabFocus();
					DebugLog($"Focused button at index {n}: {button.Name}");
				}
				else 
				{ 
					var firstButton = GetFirstFocusableButton(true);
					if (firstButton == null)
					{
						DebugLog("WARNING: No focusable buttons available!");
					}
				}
			}
			else
			{
				DebugLog("WARNING: Button not in tree!");
				Control focusOwner = GetViewport().GuiGetFocusOwner();
				focusOwner?.ReleaseFocus();
			}
		}
		else
		{
			DebugLog("WARNING: No buttons in menu!");
		}
	}
	
	public bool Close()
	{
		DebugLog("Close() called");
		
		if (InMenuTree())
		{
			if (index < buttons.Count)
			{
				BaseButton button = buttons[index];
				button.ReleaseFocus();
			}
			
			EmitSignal(SignalName.Closed);
			
			if (HideOnClose) 
			{ 
				Hide(); 
			}
			
			return true;
		}
		
		DebugLog("Not in menu tree, cannot close");
		return false;
	}
	
	public void Release()
	{
		DebugLog("Release() called");
		ButtonEnableFocus(false);
	}
	
	private void OnActivated()
	{
		DebugLog($"Activated - Tree count before: {tree.Count}");
		
		if (!InMenuTree())
		{
			tree.Add(this);
			DebugLog($"Added to tree - New tree count: {tree.Count}");
		}
		else
		{
			DebugLog("Already in tree");
		}
	}
	
	private void OnClosed()
	{
		DebugLog($"Closed - Tree count before: {tree.Count}");
		
		CloseMenusInFrontOfSelf();
		
		if (tree.Contains(this))
		{
			tree.Remove(this);
			DebugLog($"Removed from tree - New tree count: {tree.Count}");
		}

		if (tree.Count > 0)
		{
			var previousMenu = tree.Last();
			DebugLog($"Focusing previous menu: {previousMenu.Name}");
			previousMenu.ButtonFocus();
		}
	}
	
	private void CloseMenusInFrontOfSelf()
	{
		int treePos = tree.IndexOf(this);
		if (treePos < 0) return;

		DebugLog($"Closing menus in front of position {treePos}");
		
		// Close all menus after this one
		while (tree.Count > treePos + 1)
		{
			var menuToClose = tree.Last();
			DebugLog($"Closing menu in front: {menuToClose.Name}");
			menuToClose.Close();
		}
	}

	// Debug method to print current menu state
	public void PrintDebugInfo()
	{
		GD.Print("=== Menu Debug Info ===");
		GD.Print($"Name: {Name}");
		GD.Print($"Visible: {Visible}");
		GD.Print($"Button Count: {buttons.Count}");
		GD.Print($"Current Index: {index}");
		GD.Print($"In Tree: {InMenuTree()}");
		GD.Print($"Tree Size: {tree.Count}");
		GD.Print("Buttons:");
		for (int i = 0; i < buttons.Count; i++)
		{
			var btn = buttons[i];
			GD.Print($"  [{i}] {btn.Name} - Visible: {btn.Visible}, Disabled: {btn.Disabled}, HasFocus: {btn.HasFocus()}");
		}
		GD.Print("======================");
	}

	// Additional helper method for Node2D specific functionality
	public void SetMenuPosition(Vector2 position)
	{
		Position = position;
		DebugLog($"Menu position set to: {position}");
	}

	// Helper to move the entire menu
	public void MoveMenuBy(Vector2 offset)
	{
		Position += offset;
		DebugLog($"Menu moved by: {offset}, new position: {Position}");
	}
}