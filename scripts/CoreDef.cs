using Godot;
using System;
using System.Collections.Generic;

public partial class CoreDef : Node2D
{
	TurnManager _turnManager;

	public override void _Ready()
	{
		// Ensures TurnManager can locate this node when ending a round
		AddToGroup("ActiveDef"); 
		_turnManager = GetTree().GetFirstNodeInGroup("TurnManager") as TurnManager;
	}

	public override void _Process(double delta)
	{
	}

	public void Setup(Dictionary<string, Variant> coreDefData = null)
	{
		int Seed = _turnManager.Seed;
		Random random = new Random(Seed);
		_turnManager.Setup(random.Next(21));
	}
}