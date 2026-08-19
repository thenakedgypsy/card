using Godot;
using System;
using System.Collections.Generic;

public partial class CardPileDisplayButotn : Button
{
	[Export]
	public string PileName = "Deck";
	private Cardpile pile;
	private CardPileDisplay display;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		display = GetTree().GetFirstNodeInGroup("CardPileDisplay") as CardPileDisplay;
		pile = GetTree().GetFirstNodeInGroup(PileName) as Cardpile;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		Text = $"{PileName}: " + pile.GetCards().Count;
	}

	public void buttonPressed()
	{
		display.DisplayCards(pile.GetCards() as List<string>);
	}
}
