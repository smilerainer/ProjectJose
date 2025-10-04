# DialogicLayout.gd
# Minimal GDScript wrapper for Dialogic layout compatibility
# All game logic is handled by DialogueControls.cs child node
extends Control

## Required by Dialogic to identify this as a valid layout
func _get_covered_rect() -> Rect2:
	return Rect2(global_position, size)

## Called by Dialogic when the layout is ready
func _ready() -> void:
	# The C# DialogueControls child will handle initialization
	pass

## These methods might be called by Dialogic on portrait nodes
## We implement stubs to prevent errors, but the actual portraits
## will be instantiated by Dialogic and will have proper implementations
func _highlight() -> void:
	pass

func _unhighlight() -> void:
	pass

func _should_do_portrait_update(_character, _portrait: String) -> bool:
	return false

func _update_portrait(_character, _portrait: String) -> void:
	pass

func _set_mirror(_mirrored: bool) -> void:
	pass

func _set_extra_data(_extra_data: String) -> void:
	pass
