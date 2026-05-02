using Godot;
using System;
using System.Collections;
using System.Diagnostics;

public partial class Card : Node2D
{
     public enum CardType
    {
        Energy,
        Summon,
        Spell,
        Enchant
    }
    public enum Location
    {
        Deck,
        Hand,
        Discard,
        Exile,
        Unpurchased
    }

    public enum Element
    {
        Fire,
        Water,
        Wind,
        Earth  
    }


    public bool isDragging;
    public int cost;
    public Element element;
    public Location location;
    public CardType type;
    public string  cardName;


    private bool _mouseIsOver;
    private bool _isScaledUp;
    private bool _shouldReturnToHand;

    private bool _isInPlayzone;
    private bool _willPlay;
    private Vector2 _dragOffset;
    private bool _isBeingRemoved;
    private Hand _hand;
	private Discard _discard;
    private Sprite2D _art;
    private RichTextLabel _title;
    private RichTextLabel _costDisplay;
    private RichTextLabel _text;
    private RichTextLabel _typeDisplay;

    public override void _Ready()
    {
        ZIndex = 4;
		_discard = GetTree().GetFirstNodeInGroup("Discard") as Discard;
		_hand = GetTree().GetFirstNodeInGroup("Hand") as Hand;
       
        _art = GetNode<Sprite2D>("Art");
        _text = GetNode<RichTextLabel>("Text");
         _title = GetNode<RichTextLabel>("CardName");
        _costDisplay = GetNode<RichTextLabel>("Cost");
        _typeDisplay = GetNode<RichTextLabel>("Type");

        _title.Text = cardName = "Uninstantiated Card";
    }

    
    // =========================
    // CARD GENERATION
    // =========================

    public void Generate(string cardID, Location destination = Location.Unpurchased)
    {
        InstantiateArt(cardID);
        InstantiateData(cardID);

        switch (destination)
        {
            case Location.Hand:          
                _shouldReturnToHand = true; //temp
                location = Location.Hand; //temp
                _hand.AddCard(this);
                break;

            case Location.Discard:
                _discard.AddCard(this);
                break;
        }     
    }

    private void InstantiateData(string cardID)
    {
        string textPath = $"res://assets/cards/text/en_gb/{cardID}.json";       //lang stuffs
        string dataPath = $"res://assets/cards/data/{cardID}.json";

        // Load both JSON files
        var data = LoadJson(dataPath);
        var textData = LoadJson(textPath);

        if (data == null)
        {
            GD.PrintErr($"Missing card data: {dataPath}");
            return;
        }

        if (textData == null)
        {
            GD.PrintErr($"Missing card text: {textPath}");
            return;
        }

        // ===== Gameplay data =====

        cost = data.ContainsKey("cost") ? (int)data["cost"] : 0;
        _costDisplay.Text = cost.ToString();

        if (data.ContainsKey("type") &&
            Enum.TryParse(data["type"].ToString(), out CardType parsedType))
            type = parsedType;
            _typeDisplay.Text = type.ToString();

        if (data.ContainsKey("element") &&
            Enum.TryParse(data["element"].ToString(), out Element parsedElement))
            element = parsedElement;

        // ===== Text data =====

        cardName = textData.ContainsKey("name") ? textData["name"].ToString() : "Unnamed";
        _title.Text = cardName;
        _text.Text = textData.ContainsKey("text") ? textData["text"].ToString() : "";
    }

    private void InstantiateArt(string cardID)
    {
        string path = $"res://assets/cards/art/{cardID}.png";

        Texture2D texture = GD.Load<Texture2D>(path);
        _art.Texture = texture;
    }

    // =========================
    // DRAG SYSTEM
    // =========================

    public void StartDrag()
    {
        isDragging = true;
        _dragOffset = GlobalPosition - GetGlobalMousePosition();

        if (!_isScaledUp)
            ScaleUp();
    }

    public void UpdateDrag(Vector2 mousePos)
    {
        if (!isDragging) return;

        GlobalPosition = mousePos + _dragOffset;
		Rotation = 0;
    }

    public void EndDrag()
    {
        isDragging = false;

        if (!_mouseIsOver && _isScaledUp)
            ScaleDown();

        if (_isInPlayzone)
        {
            Play();
        }
    }

    // =========================
    // GAME LOGIC
    // =========================

    public void Play()
    {
        GD.Print($"{cardName} played");

        _willPlay = false;
        _shouldReturnToHand = false;
        _hand.QueueRemoveCard(this);

		Discard();
    }

	public void Discard()
	{
		GD.Print($"{cardName} moved to _discard");
		_discard.AddCard(this);
        location = Location.Discard;
	}

	public void AddToHand()
	{
		GD.Print($"{cardName} moved to _hand");
		_shouldReturnToHand = true;
		_hand.AddCard(this);
        location = Location.Hand;
	}

    public void Exile()
    {
        location = Location.Exile;
    }

    public void Remove()
    {
        if (_isBeingRemoved) return;
        _isBeingRemoved = true;

		GD.Print($"{Name} removed from existance");
        QueueFree();
    }

    public void EnterPlayZone()
    {
        _isInPlayzone = true;
    }

    public void ExitPlayZone()
    {
        _isInPlayzone = false;
    }

    // =========================
    // MOUSE VISUALS
    // =========================

    public void MouseOver()
    {
        _mouseIsOver = true;

        if (!_isScaledUp && !isDragging)
            ScaleUp();
    }

    public void MouseOff()
    {
        _mouseIsOver = false;

        if (!isDragging && _isScaledUp)
            ScaleDown();
    }

    public void ScaleUp()
    {
        if (_isScaledUp) return;

        Scale *= 1.2f;
        Position -= new Vector2(0f, 50f);
        ZIndex = 1000;

        _isScaledUp = true;
    }

    public void ScaleDown()
    {
        if (!_isScaledUp) return;

        Scale /= 1.2f;
        Position += new Vector2(0f, 50f);
        ZIndex = 4;

        _isScaledUp = false;
    }

    // =========================
    // HELPERS
    // =========================
    private Godot.Collections.Dictionary LoadJson(string path)
    {
        if (!FileAccess.FileExists(path))
            return null;

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        string jsonText = file.GetAsText();

        var json = new Json();
        if (json.Parse(jsonText) != Error.Ok)
        {
            GD.PrintErr($"JSON parse error in {path}: {json.GetErrorMessage()}");
            return null;
        }

        return json.Data.AsGodotDictionary();
    }
}