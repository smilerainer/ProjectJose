using System;
using System.Collections.Generic;
using Godot;

public class Entity
{
	public int Index { get; set; }
	public string Name { get; set; }
	public bool IsVisible { get; set; }
	public bool IsFocused { get; set; }
	public int CoordinateX { get; set; }
	public int CoordinateY { get; set; }


	public float Speed { get; set; }
	public float HealthPoints { get; set; }
	public float SanityPoints { get; set; }
}

public class Option
{
	public string Name { get; set; }
	public int DamageToTarget { get; set; }
	public int DamageToSelf { get; set; }
	public string TargetingType { get; set; }
}

public class Player : Entity
{
	public List<Option> Options { get; set; }
}