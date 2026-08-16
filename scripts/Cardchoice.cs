using Godot;
using System;
using System.Collections.Generic;

public partial class Cardchoice : Node2D
{
    [Export] public PackedScene CardPrefab;
    
    // Positions for the 3 generated cards. Adjust these values in the editor or leave as defaults.
    [Export] public Vector2[] CardPositions = new Vector2[3] 
    { 
        new Vector2(-250, 0), 
        new Vector2(0, 0), 
        new Vector2(250, 0) 
    };

    private CardPool _cardPool;
    private Node _deck; // Assuming a Deck class exists based on CardManager
    private List<Card> _displayedCards = new List<Card>();
	private Overworld _overworld;

    public override void _Ready()
    {
        // Move this node to the center of the screen when it spawns
        Position = GetViewportRect().Size / 2f;

        // Fetch dependencies using your group architecture
        _cardPool = GetTree().GetFirstNodeInGroup("CardPool") as CardPool;
        _deck = GetTree().GetFirstNodeInGroup("Deck") as Node;
		_overworld = GetTree().GetFirstNodeInGroup("Overworld") as Overworld;

        if (_cardPool == null || CardPrefab == null)
        {
            GD.PrintErr("Cardchoice missing CardPool or CardPrefab!");
            return;
        }

        GenerateChoices();
    }

    public void GenerateChoices()
    {
        for (int i = 0; i < 3; i++)
        {
            // Use your unfinished PullRandomCard method
            int randomSeed = (int)GD.Randi();
            string randomCardId = _cardPool.PullRandomCard(randomSeed);

            // Instantiate and set up the card
            Card newCard = CardPrefab.Instantiate() as Card;
            AddChild(newCard);
            
            // Hydrate data using your existing method
            newCard.Generate(randomCardId);
            
            // Set the location so the card knows it shouldn't act like a hand card
            newCard.location = Card.Location.Unpurchased;

            // Position it
            if (i < CardPositions.Length)
            {
                newCard.Position = CardPositions[i];
            }

            // Subscribe to our new click signal
            newCard.CardClicked += OnCardSelected;
            
            _displayedCards.Add(newCard);
        }
    }

    private void OnCardSelected(Card selectedCard)
    {
        GD.Print($"Card chosen: {selectedCard.cardName}");
        
        // Add the string ID to your deck. 
        // Note: Change "AddCard" to match the exact method name inside your Deck.cs script.
        if (_deck != null)
        {
            _deck.Call("AddCard", selectedCard.CardID); 
        }

        // Clean up the instantiated visual cards
        foreach (Card card in _displayedCards)
        {
            card.CardClicked -= OnCardSelected; // Good practice to unsubscribe
            card.QueueFree();
        }
        _displayedCards.Clear();
		_overworld.InScene = false;
        // Close the choice menu
        QueueFree();
    }
}