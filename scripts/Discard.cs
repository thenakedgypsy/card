using Godot;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;

public partial class Discard : Cardpile
{

    public override void _Ready()
    {
        base._Ready();
    }

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
    {
        
    }

    public override void AddCard(Card card)
    {
        base.AddCard(card);
        card.Position = Position;
    }

    public override void RemoveCard(Card card)
    {
        base.RemoveCard(card);
    }



}