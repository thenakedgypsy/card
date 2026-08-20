using Godot;
using System;
using System.ComponentModel;

public partial class Overworld : Node2D
{
	[Export]
	public int Seed = 0;
	public bool InScene = false;
	[Export]
	public int roundNum = 0;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{

	}

	public void GenerateSeed()
	{
		if (Seed == 0)
		{
			Random random = new Random();
			Seed = random.Next();
		}
	}


	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
