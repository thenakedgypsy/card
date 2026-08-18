using Godot;

public partial class CardManager : Node
{
    [Export] public PackedScene CardPrefab;
    
    private Deck _deck;
    private Discard _discard;
    private Hand _hand;
    private Hand ActiveHand
    {
        get
        {
            // If we don't have the reference yet, find it now
            if (_hand == null)
            {
                _hand = GetTree().GetFirstNodeInGroup("Hand") as Hand;
            }
            return _hand;
        }
    }

    private Discard ActiveDiscard
    {
        get
        {
            // If we don't have the reference yet, find it now
            if (_discard == null)
            {
                _discard = GetTree().GetFirstNodeInGroup("Discard") as Discard;
            }
            return _discard;
        }
    }

    public override void _Ready()
    {
        _deck = GetTree().GetFirstNodeInGroup("Deck") as Deck;
        _discard = GetTree().GetFirstNodeInGroup("Discard") as Discard;

    }

    /// <summary>
    /// Resets the card piles and clears cached references for a new game.
    /// Call this before cleanup.
    /// </summary>
    public void Reset()
    {
        // Ensure deck reference is valid
        if (_deck == null)
        {
            _deck = GetTree().GetFirstNodeInGroup("Deck") as Deck;
        }

        // Use ActiveDiscard to safely grab discard if it's currently null
        if (ActiveDiscard != null && ActiveDiscard.GetNumCards() > 0)
        {
            GD.Print("Resetting game: moving all cards from discard back to deck...");
            _deck.AddCards(ActiveDiscard.GetCards());
            ActiveDiscard.Clear();
        }

        foreach (Node child in ActiveHand.GetChildren())
        {
            if (child is Card card)
            {
                _deck.AddCard(card.CardID);
                card.QueueFree();
            }
        }

        if (_deck != null)
        {
            _deck.Shuffle();
        }

        // Reset our cached references to hand and discard
        _hand = null;
        _discard = null;
    }

    public void DrawCard()
    {
        // 1. Check if deck is empty and needs a reshuffle
        if (_deck.GetNumCards() == 0)
        {
            if (ActiveDiscard.GetNumCards() == 0)
            {
                GD.Print("Both Deck and Discard are empty! Cannot draw.");
                return;
            }

            GD.Print("Deck is empty! Shuffling discard pile into deck...");
            
            // Move all cards from discard to deck
            _deck.AddCards(ActiveDiscard.GetCards());
            ActiveDiscard.Clear();
            
            // Shuffle the replenished deck
            _deck.Shuffle();
        }

        // 2. Proceed with drawing
        string drawnId = _deck.DrawTopCard();
        
        // 3. Create the Godot node
        Card newCard = CardPrefab.Instantiate<Card>();
        ActiveHand.AddChild(newCard); 
        
        // 4. Hydrate the ID and data
        newCard.Generate(drawnId);
        
        // 5. Subscribe to the signals
        newCard.CardPlayed += OnCardPlayed;
        newCard.CardDiscarded += OnCardDiscarded;

        // 6. Send to the visual hand
        ActiveHand.AddCard(newCard);
    }

    private void OnCardPlayed(Card card)
    {
        // Add string ID to discard
        ActiveDiscard.AddCard(card.CardID);
        
        // Clean up the Godot node
        ActiveHand.RemoveCard(card);
        card.QueueFree();
    }

    private void OnCardDiscarded(Card card)
    {
        ActiveDiscard.AddCard(card.CardID);
        ActiveHand.RemoveCard(card);
        card.QueueFree();
    }
}