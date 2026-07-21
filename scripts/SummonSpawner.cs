using Godot;
using System;
using System.Collections.Generic;

public partial class SummonSpawner : Node2D
{

    private Sprite2D _sprite;
    private Summon _summon;
    private Dictionary<string, Variant> _data;
    private string _summonID;
    private Card.Element _element;
    private bool _readyToPlace;
    private Mouse _mouse;
    private TurnManager _turnManager;
    private Board _board;

    public override void _Ready()
    {
        _readyToPlace = true;
        _mouse = GetTree().GetFirstNodeInGroup("Mouse") as Mouse;
        _turnManager = GetTree().GetFirstNodeInGroup("TurnManager") as TurnManager;
        _board = GetTree().CurrentScene.GetNode<Node2D>("Board") as Board;
    }

    public override void _Process(Double delta)
    {        
        if (_readyToPlace)
        {
            Vector2I cell = _turnManager.WorldToCell(GetGlobalMousePosition());
            GlobalPosition = _turnManager.CellToWorld(cell);
            CheckInput();
        }

    }

    public void CheckInput()
    {
        if (Input.IsActionJustPressed("lClick") && CheckPlacement())
        {
            Place();
        }
        else if (Input.IsActionJustPressed("lClick"))
        {
            FlashRed();
            GD.Print("RED");
        }
    }

    public bool CheckPlacement()
    {
        Vector2I cell = _turnManager.WorldToCell(GetGlobalMousePosition());
        if (_board.IsCellWalkable(cell) && !_turnManager.IsSolidCell(cell))
        {
            return true;
        }
        return false;
    }

    public async void FlashRed()
    {
        Color original = SelfModulate;

        Tween tween = CreateTween();

        // Flash red
        tween.TweenProperty(_sprite, "self_modulate", Colors.Red, 0.1f);

        // Return to original color
        tween.TweenProperty(_sprite, "self_modulate", new Color(1f, 1f, 1f, 0.5f), 0.1f);

        await ToSignal(tween, Tween.SignalName.Finished);
    }

    public void Setup(Card.Element ele, Dictionary<string, Variant> data, string summonID)
    {
        _sprite = GetNode<Sprite2D>("Sprite2D");
        string path = $"res://assets/summons/{summonID}.png";
        Texture2D texture = GD.Load<Texture2D>(path);
    
        _sprite.Texture = texture;
        _sprite.SelfModulate = new Color(1f, 1f, 1f, 0.5f);
    
        // Match the visual offset logic from Summon.cs
        Vector2 spriteSize = texture.GetSize() * _sprite.Scale;
        if (_sprite.Centered)
        {
            _sprite.Offset = new Vector2(0, 16f - (spriteSize.Y * 0.5f));
        }
        else
        {
            _sprite.Offset = new Vector2(0, 16f - spriteSize.Y);
        }
    
        _data = data;
        _summonID = summonID;
        _element = ele;
    
        ZIndex = 3;
        _readyToPlace = true;
    }

    public void Place()
    {
        PackedScene scene = GD.Load<PackedScene>("res://prefabs/Summon.tscn");
        Summon summon = scene.Instantiate() as Summon;
        _board.AddChild(summon);
        summon.Generate(_element, _data, _summonID);

        Vector2I cell = _turnManager.WorldToCell(GlobalPosition);
        summon.GlobalPosition = _turnManager.CellToWorld(cell);

        _turnManager.RebakeNav();
        _readyToPlace = false;

        QueueFree();
    }

}