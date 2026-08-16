using Godot;
using System.Collections.Generic;

public partial class Hand : Node2D // No longer inherits from Cardpile
{
    [Export] public float CardSpacing = 150f;
    [Export] public float FanHeight = 40f;

    // Manages its own active node list
    private List<Card> cardsInHand = new List<Card>();
    private List<Card> _pendingRemoval = new List<Card>();

    public override void _Process(double delta)
    {
        ProcessRemovals();
        PositionHand();
    }

    // =========================
    // CARD MANAGEMENT
    // =========================

    public void AddCard(Card card)
    {
        cardsInHand.Add(card);
        card.CallDeferred("reparent", this);
        UpdateHand();
    }

    public void RemoveCard(Card card)
    {
        if (!cardsInHand.Contains(card)) return;
        cardsInHand.Remove(card);
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
            if (cardsInHand.Contains(card))
            {
                RemoveCard(card);
                GD.Print(card.cardName, " removed from hand visual");
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
        if (cardsInHand.Count == 0) return;

        float totalWidth = (cardsInHand.Count - 1) * CardSpacing;

        for (int i = 0; i < cardsInHand.Count; i++)
        {
            Card card = cardsInHand[i];

            // Don't fight the mouse
            if (card.isDragging) continue;

            float x = i * CardSpacing - totalWidth / 2f;
            float y = Mathf.Abs(i - cardsInHand.Count / 2f) * FanHeight;

            Vector2 targetPos = new Vector2(x, y);

            // Smooth movement
            card.Position = card.Position.Lerp(targetPos, 0.2f);

            // Optional fan rotation
            float angle = (i - (cardsInHand.Count - 1) / 2f) * 0.05f;
            card.Rotation = angle;
        }
    }

    public int GetNumCards()
    {
        return cardsInHand.Count;
    }
}