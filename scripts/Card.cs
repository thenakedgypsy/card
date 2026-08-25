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
    private Overworld _overworld;
    

    public override void _Ready()
    {
        ZIndex = 4;
        
        // Removed _discard and _hand fetches. The card doesn't need to know about them anymore.
        _turnManager = GetTree().GetFirstNodeInGroup("TurnManager") as TurnManager;
        _energyManager = GetTree().GetFirstNodeInGroup("EnergyManager") as EnergyManager;
        _overworld = GetTree().GetFirstNodeInGroup("Overworld") as Overworld;
       
        _art = GetNode<Sprite2D>("Art");
        _text = GetNode<RichTextLabel>("Text");
        _title = GetNode<RichTextLabel>("CardName");
        _costDisplay = GetNode<RichTextLabel>("Cost");
        _typeDisplay = GetNode<RichTextLabel>("Type");
        _frame = GetNode<Sprite2D>("Frame");
        _rarityFrame = GetNode<Sprite2D>("Rarity");
      
        _title.Text = cardName = "Uninstantiated Card";
    }

    public override void _Process(double delta)
    {
        CheckRedOutMana();
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

    public void CheckRedOutMana()
    {
        if (_overworld.InScene && _overworld.sceneType == OverworldNode.Type.CoreDefence)
        {
            if (_energyManager.CurrentEnergy[element] < cost)
            {
                _costDisplay.Text = "[color=red]" + cost;
            }
            else
            {
                _costDisplay.Text = "[color=white]" + cost;
            }
        }
        else
        {
            _costDisplay.Text = "[color=white]" + cost;
        }
    }
    


    private void InstantiateData(string cardID)
    {     
        var data = CardCombiner.GetCardData(cardID);
        //lang stuffs

        if (data == null)
        {
            string dataPath = $"res://assets/cards/data/{cardID}.json";
            // Load both JSON files
            data = LoadJson(dataPath);
            if (data == null)
            {
                GD.PrintErr($"Missing card data: {dataPath}");
                return;
            }
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

        cardName = data.ContainsKey("name") ? data["name"].ToString() : "Unnamed";
        _title.Text = cardName;
        
        _text.Text = CardTextBuilder.BuildCardText(cardID);

        //----- Effect Data -----

        if (data.ContainsKey("effects") && data["effects"].VariantType == Variant.Type.Array)
        {
            PackedScene scene = GD.Load<PackedScene>("res://prefabs/CardEffect.tscn");
            _effect = scene.Instantiate() as CardEffect;
            AddChild(_effect);

            if (type == CardType.Summon)
            {
                // Pass the top-level card dictionary so the spawner gets health, range, and effects
                _effect.ConstructEffect(element, new Godot.Collections.Dictionary<string, Variant>(data), cardID);
            }
            else
            {
                var rawEffects = data["effects"].AsGodotArray();
                var effectsList = new List<Godot.Collections.Dictionary<string, Variant>>();

                foreach (var item in rawEffects)
                {
                    effectsList.Add(item.AsGodotDictionary<string, Variant>());
                }

                _effect.ConstructSpellEffects(element, effectsList, cardID);
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
        // Fetch the data via CardCombiner
        var data = CardCombiner.GetCardData(cardID);
        
        // Default to using the cardID, but override it if an "artId" exists
        string artToLoad = cardID;
        if (data != null && data.ContainsKey("artId"))
        {
            artToLoad = data["artId"].ToString();
        }

        // Load using the resolved art ID
        string path = $"res://assets/cards/art/{artToLoad}.png";

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

// Card.cs
    public bool CanPlay()
    {
        if (_turnManager.IsResolving)
        {
            GD.Print("WARN: Cannot play card, another action is resolving.");
            return false; 
        }

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

    public Godot.Collections.Dictionary<string, Variant> LoadJson(string path)
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
        return json.Data.AsGodotDictionary<string, Variant>();
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