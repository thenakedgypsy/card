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
            if (_hand == null || !GodotObject.IsInstanceValid(_hand))
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
            if (_discard == null || !GodotObject.IsInstanceValid(_discard))
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
        if (_deck == null || !GodotObject.IsInstanceValid(_deck))
        {
            _deck = GetTree().GetFirstNodeInGroup("Deck") as Deck;
        }

        var discard = ActiveDiscard;
        if (discard != null && GodotObject.IsInstanceValid(discard) && discard.GetNumCards() > 0)
        {
            GD.Print("Resetting game: moving all cards from discard back to deck...");
            _deck.AddCards(discard.GetCards());
            discard.Clear();
        }

        var hand = ActiveHand;
        if (hand != null && GodotObject.IsInstanceValid(hand))
        {
            foreach (Node child in hand.GetChildren())
            {
                if (child is Card card && GodotObject.IsInstanceValid(card))
                {
                    _deck.AddCard(card.CardID);
                    card.QueueFree();
                }
            }
        }

        if (_deck != null && GodotObject.IsInstanceValid(_deck))
        {
            _deck.Shuffle();
        }

        _hand = null;
        _discard = null;
    }

    public void DrawCard()
    {
        if (_deck == null || !GodotObject.IsInstanceValid(_deck))
        {
            _deck = GetTree().GetFirstNodeInGroup("Deck") as Deck;
        }

        // 1. Check if deck is empty and needs a reshuffle
        if (_deck.GetNumCards() == 0)
        {
            var discard = ActiveDiscard;
            if (discard == null || !GodotObject.IsInstanceValid(discard) || discard.GetNumCards() == 0)
            {
                GD.Print("Both Deck and Discard are empty! Cannot draw.");
                return;
            }

            GD.Print("Deck is empty! Shuffling discard pile into deck...");
            
            _deck.AddCards(discard.GetCards());
            discard.Clear();
            _deck.Shuffle();
        }

        // 2. Proceed with drawing
        string drawnId = _deck.DrawTopCard();
        
        // 3. Create the Godot node
        Card newCard = CardPrefab.Instantiate<Card>();
        var hand = ActiveHand;
        if (hand != null && GodotObject.IsInstanceValid(hand))
        {
            hand.AddChild(newCard); 
            
            // 4. Hydrate the ID and data
            newCard.Generate(drawnId);
            
            // 5. Subscribe to the signals
            newCard.CardPlayed += OnCardPlayed;
            newCard.CardDiscarded += OnCardDiscarded;

            // 6. Send to the visual hand
            hand.AddCard(newCard);
        }
    }

    private void OnCardPlayed(Card card)
    {
        var discard = ActiveDiscard;
        if (discard != null && GodotObject.IsInstanceValid(discard))
        {
            discard.AddCard(card.CardID);
        }
        
        var hand = ActiveHand;
        if (hand != null && GodotObject.IsInstanceValid(hand))
        {
            hand.RemoveCard(card);
        }
        card.QueueFree();
    }

    private void OnCardDiscarded(Card card)
    {
        var discard = ActiveDiscard;
        if (discard != null && GodotObject.IsInstanceValid(discard))
        {
            discard.AddCard(card.CardID);
        }
        
        var hand = ActiveHand;
        if (hand != null && GodotObject.IsInstanceValid(hand))
        {
            hand.RemoveCard(card);
        }
        card.QueueFree();
    }
}