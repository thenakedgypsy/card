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

    public void DrawCard()
    {
        // 1. Check if deck is empty and needs a reshuffle
        if (_deck.GetNumCards() == 0)
        {
            if (_discard.GetNumCards() == 0)
            {
                GD.Print("Both Deck and Discard are empty! Cannot draw.");
                return;
            }

            GD.Print("Deck is empty! Shuffling discard pile into deck...");
            
            // Move all cards from discard to deck
            _deck.AddCards(_discard.GetCards());
            _discard.Clear();
            
            // Shuffle the replenished deck
            _deck.Shuffle();
        }

        // 2. Proceed with drawing
        string drawnId = _deck.DrawTopCard();
        
        // 3. Create the Godot node
        Card newCard = CardPrefab.Instantiate<Card>();
        AddChild(newCard); 
        
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