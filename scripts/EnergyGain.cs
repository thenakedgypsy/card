using Godot;
using System;

public partial class EnergyGain : Node2D
{
	EnergyManager energyManager;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		energyManager = GetTree().GetFirstNodeInGroup("EnergyManager") as EnergyManager;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void ButtonPressed(Card.Element element)
	{
		energyManager.TryGainRegen(1, element);
		Overworld overworld = GetTree().GetFirstNodeInGroup("Overworld") as Overworld;
		overworld.InScene = false;
		QueueFree();
	}
}
