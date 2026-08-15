using Godot;
using System;
using System.Collections.Generic;

public partial class CoreDef : Node2D
{
	TurnManager _turnManager;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_turnManager = GetTree().GetFirstNodeInGroup("TurnManager") as TurnManager;		
		Setup(new Dictionary<string, Variant>());
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void Setup(Dictionary<string, Variant> coreDefData)
	{
		//int numEnemies = (int)coreDefData["numEnemies"];
		Random random = new Random();	
		_turnManager.Setup(random.Next(61));
	}


}
