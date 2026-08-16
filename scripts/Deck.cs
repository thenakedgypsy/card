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
		AddCard("blockOfIce");
		AddCard("blockOfIce");
		AddCard("windturret");
		AddCard("windturret");
		AddCard("windturret");
		AddCard("fireturret");
		AddCard("fireturret");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}


	private void PositionDeck()
	{
	}


}
