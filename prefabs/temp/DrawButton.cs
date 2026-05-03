using Godot;
using System;

public partial class DrawButton : Button
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void Press()
	{
		PackedScene scene = GD.Load<PackedScene>("res://prefabs/Card.tscn");
		Card card = scene.Instantiate() as Card;

		AddChild(card);

		Random random = new Random();

		
		if (random.Next(2) > 0)
		{
			card.Generate("fireball", Card.Location.Hand);
		}
		else
		{
			card.Generate("energy_red", Card.Location.Hand);
		}
		
	}
}
