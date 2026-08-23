using Godot;
using System;
using System.Collections.Generic;

public partial class Deck : Cardpile
{
	CardPileDisplay display;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
        base._Ready();
		AddCard("blockOfIce");
		AddCard("blockOfIce");
		AddCard("earthturret");
		AddCard("earthturret");
		AddCard("fireball");
		AddCard("fireball");
		AddCard("windturret");
		AddCard("windturret");

	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}


	private void PositionDeck()
	{
	}

}
