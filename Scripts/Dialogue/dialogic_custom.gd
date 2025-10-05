@tool
extends DialogicPortrait

@export_file var image := ""

func _update_portrait(passed_character:DialogicCharacter, passed_portrait:String) -> void:
	apply_character_and_portrait(passed_character, passed_portrait)
	apply_texture($Portrait, image)
	
	var sprite = $Portrait as Sprite2D
	if sprite and sprite.texture:
		sprite.scale = Vector2.ONE
		sprite.centered = false
		sprite.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		sprite.position = Vector2.ZERO
	
	self.clip_contents = true
