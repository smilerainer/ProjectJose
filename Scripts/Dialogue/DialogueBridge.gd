extends Node

var csharp_parent = null

func _change_portrait(portrait_name: String):
    if csharp_parent:
        csharp_parent.OnPortraitChanged(portrait_name)

func join_character():
    if csharp_parent:
        csharp_parent.OnCharacterJoined("")
    
func leave_character():
    if csharp_parent:
        csharp_parent.OnCharacterLeft("")

func update_portrait(info: Dictionary):
    if csharp_parent and info.has("portrait"):
        csharp_parent.OnPortraitChanged(info["portrait"])