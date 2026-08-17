using Godot;
using System.Collections.Generic;

public partial class CardPileDisplay : ScrollContainer
{
    [Export] private PackedScene cardScene; // Assign your Card.tscn here in the Inspector
    [Export] private int columns = 4;
    [Export] private float xSpacing = 190f; // Increased for more horizontal space
    [Export] private float ySpacing = 250f; // Increased for more vertical space
    [Export] private float padding = 40f;   // Increased outer padding
    [Export] private float cardWidth = 140f;
    [Export] private float cardHeight = 200f;
    public bool IsDisplaying;

    private Control _contentContainer;
    private List<Card> _spawnedCards = new List<Card>();

    public override void _Ready()
    {
        // Ensure a content container exists inside the ScrollContainer
        _contentContainer = GetNodeOrNull<Control>("ContentContainer");
        if (_contentContainer == null)
        {
            _contentContainer = new Control
            {
                Name = "ContentContainer",
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            AddChild(_contentContainer);
        }

        // Center the content container horizontally inside the ScrollContainer
        _contentContainer.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
    }

    /// <summary>
    /// Displays a collection of card IDs in a scrollable, centered grid.
    /// </summary>
    public void DisplayCards(List<string> cardIds)
    {
        if (IsDisplaying)
        {
            ClearDisplay();
            return;
        }
        ClearDisplay();

        // Ensure _contentContainer exists even if called before _Ready()
        if (_contentContainer == null)
        {
            _contentContainer = GetNodeOrNull<Control>("ContentContainer");
            if (_contentContainer == null)
            {
                _contentContainer = new Control
                {
                    Name = "ContentContainer",
                    MouseFilter = Control.MouseFilterEnum.Stop
                };
                AddChild(_contentContainer);
            }
            _contentContainer.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        }

        if (cardScene == null)
        {
            cardScene = GD.Load<PackedScene>("res://scenes/Card.tscn");
            if (cardScene == null)
            {
                GD.PrintErr("CardPileDisplay: Card Scene is not assigned!");
                return;
            }
        }

        int index = 0;
        foreach (string cardId in cardIds)
        {
            Card card = cardScene.Instantiate<Card>(); // Instantiates the Card node
            _contentContainer.AddChild(card);
            
            // Generate data and visuals for the card
            card.Generate(cardId);

            // Calculate grid positions (Rows and Columns)
            int row = index / columns;
            int col = index % columns;

            float posX = padding + (col * xSpacing) + (cardWidth / 2f);
            float posY = padding + (row * ySpacing) + (cardHeight / 2f);

            card.Position = new Vector2(posX, posY);

            _spawnedCards.Add(card);
            GD.Print("displaying ", cardId);
            index++;
        }

        // Update container bounds so the scrollbars calculate correctly
        int totalRows = Mathf.CeilToInt((float)index / columns);
        float totalHeight = (padding * 2) + (totalRows * ySpacing);
        float totalWidth = (padding * 2) + (columns * xSpacing);

        _contentContainer.CustomMinimumSize = new Vector2(totalWidth, totalHeight);
        IsDisplaying = true;
    }

    /// <summary>
    /// Clears all currently displayed cards from the pile view.
    /// </summary>
    public void ClearDisplay()
    {
        IsDisplaying = false;
        foreach (var card in _spawnedCards)
        {
            if (IsInstanceValid(card))
            {
                card.QueueFree();
            }
        }
        _spawnedCards.Clear();

        if (_contentContainer != null)
        {
            _contentContainer.CustomMinimumSize = Vector2.Zero;
        }
    }
}