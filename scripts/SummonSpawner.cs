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


    public override void _Ready()
    {
        _readyToPlace = true;
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
        if (Input.IsActionJustPressed("lClick"))
        {
            Place();
        }
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
        NavigationRegion2D nav = GetTree().CurrentScene.GetNode<NavigationRegion2D>("Board/Nav");
        Summon summon = scene.Instantiate() as Summon;
        nav.AddChild(summon);       
        summon.Generate(_element, _data, _summonID);
        summon.GlobalPosition = GlobalPosition;
        nav.BakeNavigationPolygon();
        _readyToPlace = false;

        QueueFree();
    }

}