using Godot;
using System;
using System.Collections.Generic;

public partial class Card : Node2D
{
    // --- Added Signals ---
    [Signal] public delegate void CardPlayedEventHandler(Card card);
    [Signal] public delegate void CardDiscardedEventHandler(Card card);
    [Signal] public delegate void CardClickedEventHandler(Card card);

    public enum CardType { Energy, Summon, Spell, Enchant }
    public enum Location { Deck, Hand, Discard, Exile, Unpurchased }
    public enum Element { Fire, Water, Wind, Earth, Neutral }
    public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }
    
    public string CardID; // NEW: Storing the ID for string-based piles
    
    public bool isDragging;
    public int cost;
    public Element element;
    public Location location;
    public CardType type;
    public string cardName;

    private bool _mouseIsOver;
    private bool _isScaledUp;
    private bool _shouldReturnToHand;

    private bool _isInPlayzone;
    private Vector2 _dragOffset;
    private bool _isBeingRemoved;
    
    // Removed Hand and Discard node references here
    private Sprite2D _art;
    private Sprite2D _frame;
    private Sprite2D _rarityFrame;
    private RichTextLabel _title;
    private RichTextLabel _costDisplay;
    private RichTextLabel _text;
    private RichTextLabel _typeDisplay;
    private TurnManager _turnManager;
    private EnergyManager _energyManager;
    private CardEffect _effect;
    private Rarity _rarity;
    private ShaderMaterial _rarityShader;
    

    public override void _Ready()
    {
        ZIndex = 4;
        
        // Removed _discard and _hand fetches. The card doesn't need to know about them anymore.
        _turnManager = GetTree().GetFirstNodeInGroup("TurnManager") as TurnManager;
        _energyManager = GetTree().GetFirstNodeInGroup("EnergyManager") as EnergyManager;
       
        _art = GetNode<Sprite2D>("Art");
        _text = GetNode<RichTextLabel>("Text");
        _title = GetNode<RichTextLabel>("CardName");
        _costDisplay = GetNode<RichTextLabel>("Cost");
        _typeDisplay = GetNode<RichTextLabel>("Type");
        _frame = GetNode<Sprite2D>("Frame");
        _rarityFrame = GetNode<Sprite2D>("Rarity");
      
        _title.Text = cardName = "Uninstantiated Card";
    }
    
    // =========================
    // CARD GENERATION
    // =========================

    public void Generate(string cardID)
    {
        CardID = cardID; // Save the string ID
        InstantiateData(cardID);
        InstantiateArt(cardID);
        // Destination routing logic removed. The new CardManager handles adding to Hand.
    }

    private void InstantiateData(string cardID)
    {
        // ... (Keep EXACTLY as it was in your original code) ...
        // Everything inside your existing InstantiateData remains the same.
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

        if (data.ContainsKey("type") &&
            Enum.TryParse(data["type"].ToString(), out CardType parsedType))
            type = parsedType;
            _typeDisplay.Text = type.ToString();

        if (data.ContainsKey("element") &&
            Enum.TryParse(data["element"].ToString(), out Element parsedElement))
            element = parsedElement;

        if (data.ContainsKey("rarity") &&
            Enum.TryParse(data["rarity"].ToString(), out Rarity parsedRarity))
            _rarity = parsedRarity;

        cost = data.ContainsKey("cost") ? (int)data["cost"] : 0;
        _costDisplay.Text = cost.ToString();
        if (type == CardType.Energy)
        {
            _costDisplay.Visible = false;
        }

        // ===== Text data =====

        cardName = textData.ContainsKey("name") ? textData["name"].ToString() : "Unnamed";
        _title.Text = cardName;
        
        string formattedText = textData.ContainsKey("text") ? textData["text"].ToString() : "";
        
        // Replace placeholders from either the root effects array or nested effect data.
        if (data.ContainsKey("effects"))
            formattedText = ReplacePlaceholders(formattedText, data["effects"]);
        if (data.ContainsKey("effectData"))
            formattedText = ReplacePlaceholders(formattedText, data["effectData"]);
           
        // Assign to RichTextLabel
        _text.Text = formattedText;

        //----- Effect Data -----

        if (data.ContainsKey("effects") || data.ContainsKey("effectData"))
        {
            PackedScene scene = GD.Load<PackedScene>("res://prefabs/CardEffect.tscn");
            _effect = scene.Instantiate() as CardEffect;
            AddChild(_effect);
            
            // Check if card has an array of spell effects or is a single Summon effect
            if (data.ContainsKey("effects") && data["effects"].VariantType == Variant.Type.Array)
            {
                var rawEffects = data["effects"].AsGodotArray();
                var effectsList = new List<Godot.Collections.Dictionary<string, Variant>>();
                
                foreach (var item in rawEffects)
                {
                    effectsList.Add(item.AsGodotDictionary<string, Variant>());
                }
                
                _effect.ConstructSpellEffects(element, effectsList, cardID);
            }
            else if (data["effectData"].VariantType == Variant.Type.Dictionary)
            {
                // Single effect fallback (e.g. Summon cards)
                var effectDict = data["effectData"].AsGodotDictionary<string, Variant>();
                _effect.ConstructEffect(element, effectDict, cardID);
            }
        }
    }

    private string ReplacePlaceholders(string text, Variant value)
    {
        if (value.VariantType == Variant.Type.Dictionary)
        {
            foreach (var entry in value.AsGodotDictionary())
            {
                text = ReplacePlaceholders(text, entry.Value);
                if (entry.Value.VariantType != Variant.Type.Dictionary &&
                    entry.Value.VariantType != Variant.Type.Array)
                {
                    text = text.Replace($"{{{entry.Key}}}", entry.Value.ToString());
                }
            }
        }
        else if (value.VariantType == Variant.Type.Array)
        {
            foreach (var item in value.AsGodotArray())
                text = ReplacePlaceholders(text, item);
        }

        return text;
    }

    private void InstantiateArt(string cardID)
    {
        // ... (Keep EXACTLY as it was in your original code) ...
        string path = $"res://assets/cards/art/{cardID}.png";

        Texture2D texture = GD.Load<Texture2D>(path);
        Texture2D frame = GD.Load<Texture2D>($"res://assets/cards/cardFrames/{element}.png");
        Texture2D rarityFrameTexture = GD.Load<Texture2D>($"res://assets/cards/cardFrames/rarityFrames/Common.png");

        _art.Texture = texture;
        _frame.Texture = frame;
        _rarityFrame.Texture = rarityFrameTexture;
        int presetIndex = _rarity switch
        {
            Rarity.Common => 1,
            Rarity.Uncommon => 2,
            Rarity.Rare => 3,
            Rarity.Epic => 4,
            Rarity.Legendary => 5,
            _ => 1
        };

        if (_rarityFrame.Material is ShaderMaterial shaderMat)
        {
            _rarityShader = (ShaderMaterial)shaderMat.Duplicate();
            _rarityFrame.Material = _rarityShader;
        }
        GD.Print("Adding shader to frame at rarity ", presetIndex);
        _rarityShader.SetShaderParameter("rarity_preset", presetIndex);
    }

    // =========================
    // DRAG SYSTEM
    // =========================
    
    // ... (Keep StartDrag, UpdateDrag, EndDrag, FlashRed EXACTLY as they were) ...
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
            if (CanPlay())
            {
                Play();
            }
            else
            {
                FlashRed();
            }
        }
    }

    public async void FlashRed()
    {
        Color original = SelfModulate;
        Tween tween = CreateTween();
        tween.TweenProperty(_frame, "self_modulate", Colors.Red, 0.25f);
        tween.TweenProperty(_frame, "self_modulate", original, 0.1f);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    // =========================
    // GAME LOGIC
    // =========================

    public void Play()
    {
        GD.Print($"{cardName} played");

        if (type == CardType.Energy)
        {
            _turnManager.PlayEnergy();
            if (element == Element.Neutral)
                _energyManager.GainEnergy(1, element);
            else
                _energyManager.TryGainRegen(1, element);
        }
        else
        {
            _effect.Trigger(); 
        }

        // Fire signal up to CardManager to handle the ID move and memory cleanup
        EmitSignal(SignalName.CardPlayed, this);
    }

    public bool CanPlay()
    {
        // ... (Keep EXACTLY as it was) ...
        if (_turnManager.State == TurnManager.GameState.PlayerTurn)
        {
            if (type == CardType.Energy)
            {
                if (_turnManager.CanPlayEnergy())
                    return true;              
                else
                    GD.Print("WARN: Cant play energy, already played one this turn");
            }
            else if (_energyManager.TrySpendEnergy(cost, element)) 
            {
                return true;
            }
            else
            {
                GD.Print("WARN: Cant play card, not enough energy");
            }
        }
        return false;
    }

    public void Discard()
    {
        GD.Print($"{cardName} moved to discard");
        EmitSignal(SignalName.CardDiscarded, this);
    }
    
    // oh the glory of removing AddToHand(), Exile(), Remove() etc. Hand/Board dragging handles hand retention natively <-- 

    public void EnterPlayZone() { _isInPlayzone = true; }
    public void ExitPlayZone() { _isInPlayzone = false; }

    // =========================
    // MOUSE VISUALS & HELPERS
    // =========================
    // ... (Keep MouseOver, MouseOff, ScaleUp, ScaleDown, LoadJson EXACTLY as they were) ...
    
    public void MouseOver()
    {
        _mouseIsOver = true;
        if (!_isScaledUp && !isDragging) ScaleUp();
    }

    public void MouseOff()
    {
        _mouseIsOver = false;
        if (!isDragging && _isScaledUp) ScaleDown();
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

    public Godot.Collections.Dictionary LoadJson(string path)
    {
        if (!FileAccess.FileExists(path)) return null;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        string jsonText = file.GetAsText();
        var json = new Godot.Json();
        if (json.Parse(jsonText) != Error.Ok)
        {
            GD.PrintErr($"JSON parse error in {path}: {json.GetErrorMessage()}");
            return null;
        }
        return json.Data.AsGodotDictionary();
    }

    public void _on_area_2d_input_event(Node viewport, InputEvent @event, int shapeIdx)
    {
        // Only allow clicking if the card is a choice menu option
        if (location == Location.Unpurchased && @event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Left && mouseEvent.Pressed)
            {
                EmitSignal(SignalName.CardClicked, this);
            }
        }
    }
}