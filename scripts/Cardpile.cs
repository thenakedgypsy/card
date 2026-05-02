using Godot;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;

public partial class Cardpile : Node2D
{
    protected List<Card> cardsInPile = new List<Card>();


	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
    {
        
    }

    public virtual void AddCard(Card card)
    {
        cardsInPile.Add(card);
    }

    public virtual void RemoveCard(Card card)
    {
       cardsInPile.Remove(card); 
    }


}