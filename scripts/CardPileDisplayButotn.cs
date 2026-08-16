using Godot;
using System;
using System.Collections.Generic;

public partial class CardPileDisplayButotn : Button
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void buttonPressed()
	{
		Deck deck = GetTree().GetFirstNodeInGroup("Deck") as Deck;
		CardPileDisplay display = GetTree().GetFirstNodeInGroup("CardPileDisplay") as CardPileDisplay;
		display.DisplayCards(deck.GetCards() as List<string>);
	}
}
