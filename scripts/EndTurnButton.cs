using Godot;
using System;

public partial class EndTurnButton : Button
{
	TurnManager _turnManager;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_turnManager = GetTree().GetFirstNodeInGroup("TurnManager") as TurnManager;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (_turnManager.State != TurnManager.GameState.PlayerTurn)
		{
			Visible = false;
		}
		else if (!Visible)
		{
			Visible = true;
		}
	}

	public void Press()
	{
		_turnManager.EndPlayerTurn();
	}
}
