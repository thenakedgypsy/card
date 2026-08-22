using Godot;
using System;
using System.Collections.Generic;

public partial class CardPicker : Node2D
{
    [Signal] public delegate void CardsCombinedEventHandler(string newCardId);

    [Export] public PackedScene CardPrefab;

    // Grid layout configuration for displaying deck cards on screen
    [Export] public int CardsPerRow = 4;
    [Export] public Vector2 CardSpacing = new Vector2(160, 220);
    [Export] public Vector2 GridOffset = new Vector2(0, 50);

    // Fixed slot positions at the top of the picker UI for selected cards
    [Export] public Vector2 Slot1Position = new Vector2(-150, -200);
    [Export] public Vector2 Slot2Position = new Vector2(150, -200);

    private Card _slot1Card = null;
    private Card _slot2Card = null;

    private readonly List<Card> _spawnedDeckCards = new List<Card>();
    private Deck _deck;

    public override void _Ready()
    {
        // Center the picker container on screen
        Position = GetViewportRect().Size / 2f;

        _deck = GetTree().GetFirstNodeInGroup("Deck") as Deck;

        if (CardPrefab == null || _deck == null)
        {
            GD.PrintErr("CardPicker: Missing CardPrefab or Deck reference!");
            return;
        }

        DisplayDeckCards();
    }

    /// <summary>
    /// Reads all card IDs directly from the player's Deck and instantiates them as choices.
    /// </summary>
    public void DisplayDeckCards()
    {
        ClearSpawnedCards();

        int totalCards = _deck.GetCards().Count;
        if (totalCards == 0)
        {
            GD.Print("CardPicker: Deck is empty!");
            return;
        }

        for (int i = 0; i < totalCards; i++)
        {
            string cardId = _deck.GetCards()[i];

            // Instantiate visual card prefab
            Card newCard = CardPrefab.Instantiate() as Card;
            AddChild(newCard);

            // Hydrate values (Element, Name, Cost, Art) and enable mouse clicking[cite: 1, 4]
            newCard.Generate(cardId);
            newCard.location = Card.Location.Unpurchased;

            // Calculate Grid Position centered on screen
            int col = i % CardsPerRow;
            int row = i / CardsPerRow;

            float startX = -((Math.Min(totalCards, CardsPerRow) - 1) * CardSpacing.X) / 2f;
            Vector2 position = new Vector2(startX + (col * CardSpacing.X), row * CardSpacing.Y) + GridOffset;
            newCard.Position = position;

            // Subscribe to click signal[cite: 1, 4]
            newCard.CardClicked += OnDeckCardClicked;

            _spawnedDeckCards.Add(newCard);
        }
    }

    private void OnDeckCardClicked(Card clickedCard)
    {
        // Slot 1 Selection: Pick the initial card from the deck
        if (_slot1Card == null)
        {
            _slot1Card = clickedCard;
            _slot1Card.Position = Slot1Position;

            // Enforce element rule: disable all cards that don't match Slot 1's element
            FilterDeckByElement(_slot1Card.element);
        }
        // Slot 2 Selection: Pick second matching-element card from the deck
        else if (_slot2Card == null && clickedCard != _slot1Card)
        {
            _slot2Card = clickedCard;
            _slot2Card.Position = Slot2Position;

            // Both valid cards chosen -> Combine them
            CombineSelectedCards();
        }
    }

    /// <summary>
    /// Filters remaining deck cards. Non-matching cards are dimmed and disabled.
    /// </summary>
    private void FilterDeckByElement(Card.Element targetElement)
    {
        foreach (Card card in _spawnedDeckCards)
        {
            if (card == _slot1Card) continue;

            if (card.element != targetElement)
            {
                // Disable input signal so non-matching cards cannot be picked[cite: 4]
                card.CardClicked -= OnDeckCardClicked;

                // Visual Feedback: Darken and turn semi-transparent
                card.Modulate = new Color(0.3f, 0.3f, 0.3f, 0.4f);
            }
        }
    }

    private void CombineSelectedCards()
    {
        if (_slot1Card == null || _slot2Card == null) return;

        // Combine using CardCombiner[cite: 2]
        string comboId = CardCombiner.CombineCards(_slot1Card.CardID, _slot2Card.CardID);

        if (!string.IsNullOrEmpty(comboId))
        {
            GD.Print($"Combined {_slot1Card.CardID} + {_slot2Card.CardID} -> {comboId}");

            // Update player's deck: remove the two base cards and insert the combined card
            _deck.RemoveCard(_slot1Card.CardID);
            _deck.RemoveCard(_slot2Card.CardID);
            _deck.AddCard(comboId);

            EmitSignal(SignalName.CardsCombined, comboId);
        }

        CleanupAndClose();
    }

    private void ClearSpawnedCards()
    {
        foreach (Card card in _spawnedDeckCards)
        {
            card.CardClicked -= OnDeckCardClicked;
            card.QueueFree();
        }
        _spawnedDeckCards.Clear();
    }

    private void CleanupAndClose()
    {
        ClearSpawnedCards();
        QueueFree();
    }
}