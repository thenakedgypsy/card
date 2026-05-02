using Godot;
using System;
using System.Collections.Generic;

public partial class Hand : Cardpile
{
    [Export] public float CardSpacing = 150f;
    [Export] public float FanHeight = 40f;
	[Export]
	public Card[] cards;

    private List<Card> _pendingRemoval = new List<Card>();

    public override void _Ready()
    {
		GD.Print("CARDS IN THIS LIST", cards);
		for(int i = 0; i < cards.Length; i++)
		{
			AddCard(cards[i]);
			GD.Print("Added card: ", cards[i]);
		}
        PositionHand();
    }

    public override void _Process(double delta)
    {
        ProcessRemovals();
        PositionHand();
    }

    // =========================
    // CARD MANAGEMENT
    // =========================

    public override void AddCard(Card card)
    {
        base.AddCard(card);
		card.hand = this;
        card.InHand = true;
        UpdateHand();
    }

    public override void RemoveCard(Card card)
    {
        if (!cardsInPile.Contains(card)) return;

        base.RemoveCard(card);

        card.InHand = false;
        card.ShouldReturnToHand = false;

        UpdateHand();
    }

    public void QueueRemoveCard(Card card)
    {
        if (!_pendingRemoval.Contains(card))
            _pendingRemoval.Add(card);
    }

    private void ProcessRemovals()
    {
        if (_pendingRemoval.Count == 0) return;

        foreach (Card card in _pendingRemoval)
        {
			if (cardsInPile.Contains(card))
			{
            	RemoveCard(card);
				GD.Print(card, " removed from hand");
			}
        }

        _pendingRemoval.Clear();
    }

    public void UpdateHand()
    {
        PositionHand();
    }

    // =========================
    // POSITIONING
    // =========================

    private void PositionHand()
    {
        if (cardsInPile.Count == 0) return;

        float totalWidth = (cardsInPile.Count - 1) * CardSpacing;

        for (int i = 0; i < cardsInPile.Count; i++)
        {
            Card card = cardsInPile[i];

            // Don't fight the mouse
            if (card.IsDragging) continue;

            float x = i * CardSpacing - totalWidth / 2f;
            float y = Mathf.Abs(i - cardsInPile.Count / 2f) * FanHeight;

            Vector2 targetPos = new Vector2(x, y);

            // Smooth movement
            card.Position = card.Position.Lerp(targetPos, 0.2f);

            // Optional fan rotation
            float angle = (i - (cardsInPile.Count - 1) / 2f) * 0.05f;
            card.Rotation = angle;
        }
    }
}