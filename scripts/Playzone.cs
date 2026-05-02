using Godot;
using System;
using System.ComponentModel;

public partial class Playzone : Node2D
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void CardEnter(Area2D area)
	{
    	var parent = area.GetParent();

    	if (parent is Card card)
    	{
    	    GD.Print("A Card Entered the playzone: " + card.Name);
			card.IsInPlayzone = true;
    	}
	}

	public void CardExit(Area2D area)
	{
		var parent = area.GetParent();
	
    	if (parent is Card card)
    	{
    	    GD.Print("A card left the playzone: " + card.Name);
			card.IsInPlayzone = false;
    	}
	}
}
