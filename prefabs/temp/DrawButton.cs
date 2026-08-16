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
		int num = random.Next(2);
		
		if (num == 5)
		{
			card.Generate("fireball");
		}
		else if (num == 5)
		{
			card.Generate("energy_red");
		}
		else if (num == 1)
		{
			card.Generate("energy_blue");
		}
		else
		{
			card.Generate("blockOfIce");
		}
		
	}
}
