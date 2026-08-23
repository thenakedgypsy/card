using Godot;
using System;
using System.Collections.Generic;

public partial class CardPicker : Node2D
{
    [Signal] public delegate void CardsCombinedEventHandler(string newCardId);

    [Export] public PackedScene CardPrefab;
    
    // Exported button that the player must press to execute the combo
    [Export] public Button CombineButton;

    // Grid layout configuration for displaying deck cards on screen
    [Export] public int CardsPerRow = 6;
    [Export] public Vector2 CardSpacing = new Vector2(160, 220);
    [Export] public Vector2 GridOffset = new Vector2(0, 50);

    // Fixed slot positions at the top of the picker UI for selected cards
    [Export] public Vector2 Slot1Position = new Vector2(-150, -200);
    [Export] public Vector2 Slot2Position = new Vector2(150, -200);

    private Card _slot1Card = null;
    private Card _slot2Card = null;

    private readonly List<Card> _spawnedDeckCards = new List<Card>();
    // Store original positions to snap cards back when unpicked
    private readonly Dictionary<Card, Vector2> _originalPositions = new Dictionary<Card, Vector2>();
    
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
        
        // Setup the combine button
        if (CombineButton != null)
        {
            CombineButton.Pressed += CombineSelectedCards;
            CombineButton.Disabled = true; // Disabled until two cards are picked
        }
        else
        {
            GD.PrintErr("CardPicker: CombineButton is not assigned in the inspector!");
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

            // Hydrate values (Element, Name, Cost, Art) and enable mouse clicking
            newCard.Generate(cardId);
            newCard.location = Card.Location.Unpurchased;

            // Calculate Grid Position centered on screen
            int col = i % CardsPerRow;
            int row = i / CardsPerRow;

            float startX = -((Math.Min(totalCards, CardsPerRow) - 1) * CardSpacing.X) / 2f;
            Vector2 position = new Vector2(startX + (col * CardSpacing.X), row * CardSpacing.Y) + GridOffset;
            
            newCard.Position = position;
            _originalPositions[newCard] = position; // Save position for unpicking

            // Subscribe to click signal
            newCard.CardClicked += OnDeckCardClicked;

            _spawnedDeckCards.Add(newCard);
        }
    }

    private void OnDeckCardClicked(Card clickedCard)
    {
        // --- UNPICKING LOGIC ---
        // If the clicked card is already in Slot 1, unpick it (and Slot 2 if occupied)
        if (clickedCard == _slot1Card)
        {
            UnpickSlot1();
            UpdateButtonState();
            return;
        }
        
        // If the clicked card is already in Slot 2, unpick just Slot 2
        if (clickedCard == _slot2Card)
        {
            UnpickSlot2();
            UpdateButtonState();
            return;
        }

        // --- PICKING LOGIC ---
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
        }
        
        UpdateButtonState();
    }

    private void UnpickSlot1()
    {
        if (_slot1Card != null)
        {
            _slot1Card.Position = _originalPositions[_slot1Card];
            _slot1Card = null;
        }

        // If we unpick the first slot, the element constraint is lifted.
        // Unpick slot 2 as well so they don't get trapped with an invalid element combo.
        if (_slot2Card != null)
        {
            UnpickSlot2();
        }

        ResetDeckFilter();
    }

    private void UnpickSlot2()
    {
        if (_slot2Card != null)
        {
            _slot2Card.Position = _originalPositions[_slot2Card];
            _slot2Card = null;
        }
    }

    private void UpdateButtonState()
    {
        if (CombineButton != null)
        {
            // Enable button only when both slots are filled
            CombineButton.Disabled = (_slot1Card == null || _slot2Card == null);
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
                // Disable input signal so non-matching cards cannot be picked
                card.CardClicked -= OnDeckCardClicked;

                // Visual Feedback: Darken and turn semi-transparent
                card.Modulate = new Color(0.3f, 0.3f, 0.3f, 0.4f);
            }
        }
    }

    /// <summary>
    /// Restores all cards to their default interactable state when Slot 1 is unpicked.
    /// </summary>
    private void ResetDeckFilter()
    {
        foreach (Card card in _spawnedDeckCards)
        {
            // Safely unsubscribe then resubscribe to avoid duplicate signal connections
            card.CardClicked -= OnDeckCardClicked;
            card.CardClicked += OnDeckCardClicked;

            // Reset visual feedback to normal
            card.Modulate = new Color(1f, 1f, 1f, 1f);
        }
    }

    private void CombineSelectedCards()
    {
        if (_slot1Card == null || _slot2Card == null) return;

        // Combine using CardCombiner
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
        _originalPositions.Clear();
    }

    private void CleanupAndClose()
    {
        ClearSpawnedCards();
        QueueFree();
    }
}