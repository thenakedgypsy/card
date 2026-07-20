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

    public override void _Ready()
    {
        _readyToPlace = true;
        _mouse = GetTree().GetFirstNodeInGroup("Mouse") as Mouse;
    }

    public override void _Process(Double delta)
    {        
        if (_readyToPlace)
        {
            GlobalPosition = GetGlobalMousePosition();
            CheckInput();
        }

    }

    public void CheckInput()
    {
        if (Input.IsActionJustPressed("lClick") && _mouse.getOverBoard())
        {
            Place();
        }
        else if (Input.IsActionJustPressed("lClick"))
        {
            FlashRed();
            GD.Print("RED");
        }
    }

    public async void FlashRed()
    {
        Color original = SelfModulate;

        Tween tween = CreateTween();

        // Flash red
        tween.TweenProperty(_sprite, "self_modulate", Colors.Red, 0.1f);

        // Return to original color
        tween.TweenProperty(_sprite, "self_modulate", original, 0.1f);

        await ToSignal(tween, Tween.SignalName.Finished);
    }

    public void Setup(Card.Element ele, Dictionary<string, Variant> data, string summonID)
    {
        _sprite = GetNode<Sprite2D>("Sprite2D");
        string path = $"res://assets/summons/{summonID}.png";
        Texture2D texture = GD.Load<Texture2D>(path);

		_sprite.Texture = texture;
        _sprite.SelfModulate = new Color(1f, 1f, 1f, 0.5f);

        _data = data;
        _summonID = summonID;
        _element = ele;

        ZIndex = 3;
        _readyToPlace = true;
    }

    public void Place()
    {
        PackedScene scene = GD.Load<PackedScene>("res://prefabs/Summon.tscn");
        Node2D board = GetTree().CurrentScene.GetNode<Node2D>("Board");
        Summon summon = scene.Instantiate() as Summon;
        board.AddChild(summon);
        summon.Generate(_element, _data, _summonID);

        Vector2I cell = TurnManager.Instance.WorldToCell(GlobalPosition);
        summon.GlobalPosition = TurnManager.Instance.CellToWorld(cell);

        TurnManager.Instance.RebakeNav();
        _readyToPlace = false;

        QueueFree();
    }

}