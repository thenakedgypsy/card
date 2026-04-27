using Godot;
using System;
using System.Collections.Generic;

public partial class Hand : Node2D
{
	[Export]
	Card[] quickAdd;
	List<Card> cardsInHand;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		cardsInHand = new List<Card>();
		if (quickAdd.Length > 0)
		{
			foreach (Card card in quickAdd)
			{
				cardsInHand.Add(card);
			}
		}

		PositionHand();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void UpdateHand()
	{
		PositionHand();
	}

	private void PositionHand()
	{
    	int count = cardsInHand.Count;
    	if (count == 0)
		{
    	    return;
		}

    	float spacing = 500f / cardsInHand.Count;  
		if (cardsInHand.Count < 3)
		{
			spacing = 80f;
		}        // horizontal spacing
    	float arcHeight = 25f;         // how much the middle lifts
    	float rotationRange = Mathf.DegToRad(20); // optional tilt

    	float centerOffset = (count - 1) / 2f;
    	float angleStep = count > 1 ? rotationRange / (count - 1) : 0;
    	float startAngle = -rotationRange / 2f;

		if (cardsInHand.Count > 1)
		{
			for (int i = 0; i < cardsInHand.Count; i++)
			{
			    Card card = cardsInHand[i];

			    if (card.WillPlay)
			    {
			        card.InHand = false;
			        cardsInHand.RemoveAt(i);
			        i--; // adjust index after removal
			        continue;
			    }

			    float t = (i - centerOffset) / centerOffset;
			    float x = (i - centerOffset) * spacing;
			    float y = -arcHeight * (1 - t * t);

			    if (!card.IsDragging && (!card.IsScaledUp || card.ShouldReturnToHand))
			    {
			        card.Position = new Vector2(x, y);

			        float angle = startAngle + angleStep * i;
			        card.Rotation = angle * 0.25f;
			    }

			    if (card.IsScaledUp)
			    {
			        card.ZIndex = cardsInHand.Count;
			    }
			    else
			    {
			        card.ZIndex = i;
			        if (card.ShouldReturnToHand)
					{
			            card.ShouldReturnToHand = false;
					}
			    }
			}
		}
		else if (cardsInHand.Count == 1)
		{
			Card card = cardsInHand[0];
			if (card.WillPlay)
			{
				//this card is going to trigger and be moved elsewhere. We need to remove it. 
				cardsInHand.Remove(card);
				card.InHand = false;
			}

			if (!card.IsDragging && (!card.IsScaledUp || card.ShouldReturnToHand))
			{
				card.GlobalPosition = GlobalPosition;
			}
			card.Rotation = 0;
		}
	}

}
