using Godot;
using System;
using System.Collections.Generic;
using System.Threading;


public partial class OverworldNode : Node2D // will be a node in the overworld tree, 
{
	public enum Type
	{
		Choice,
		CoreDefence,
		CardCombine,
		CardGain,
		EnergyGain
	}
	[Export]
	public bool isDefence;
	[Export]
	public bool isEnergy;
	[Export]
	public bool isCardGain;
	[Export]
	public bool isCardCombine;
	private bool _visitable = true;
	private bool _visisted;
	private Type _type;
	private string _title;
	private string _tooltip;
	private Sprite2D _sprite;
	private PackedScene _scene;
	private Overworld _overworld;
	public bool _mouseOver;
	public OverworldNode[] previousNodes;
	public OverworldNode[] nextNodes;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_sprite = GetNode<Sprite2D>("Sprite2D");
		_overworld = GetTree().GetFirstNodeInGroup("Overworld") as Overworld;

		if (isDefence)
		{
			buildNode(Type.CoreDefence);
		}
		else if (isEnergy)
		{
			buildNode(Type.EnergyGain);
		}
		else if (isCardGain)
		{
			buildNode(Type.CardGain);
		}
		else if (isCardCombine)
		{
			buildNode(Type.CardCombine);
		}
	}

	public void MouseOver()
	{
		_mouseOver = true;
		GD.Print("Mouse Over Node");
	}

	public void MouseOff()
	{
		_mouseOver = false;
		GD.Print("Mouse Off Node");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (_visisted)
		{
			// Visited nodes are faded out[cite: 2]
			_sprite.SelfModulate = new Color(0.8f, 0.8f, 1f, 0.2f);
		}
		else if (_visitable  && !_overworld.InScene)
		{
			// Visitable nodes are fully visible and interactive[cite: 2]
			_sprite.SelfModulate = Colors.White;
			if (_mouseOver && Input.IsActionJustPressed("lClick"))
			{
				InstantiateSceneFromNode();
			}

		}
		else
		{
			// Unvisitable nodes (past, parallel, or future locked paths) are greyed out[cite: 2]
			_sprite.SelfModulate = new Color(0.4f, 0.4f, 0.4f, 0.6f);
		}
	}

	public void buildNode(Type type, Dictionary<string, Variant> data = null)
	{	
		_type = type;	//switch for types here pass the nested data on. 
		switch (type)
		{
			case Type.CoreDefence:
				LoadCoreDefence();
				GD.Print("Defence Node Built");
				break;
			case Type.EnergyGain:
				LoadEnergyGain();
				GD.Print("EG Node Built");
				break;
			case Type.CardGain:
				LoadCardChoice();
				GD.Print("Choice Node Built");
				break;
			case Type.CardCombine:
				LoadCardCombine();
				GD.Print("Combine Node Built");
				break;

		}
	}

	public Dictionary<string, Variant> ExtractSceneData(Dictionary<string, Variant> data)
	{
		if (data == null)
		{
			GD.PushWarning("Expected Scene Data but received Null");
			return data;
		}
		var sceneVariant = data["sceneData"];
		var godotDict = sceneVariant.AsGodotDictionary();
		Dictionary<string, Variant> sceneData = new Dictionary<string, Variant>();
		foreach (var key in godotDict.Keys)
		{
			string name = key.ToString();
			var valueVar = godotDict[key];
			sceneData.Add(name, valueVar);
		}

		return sceneData;
	}

	public void LoadCardCombine()
	{
		_title = "Card Combine"; //needs lang lookup
		_tooltip = "Combine any two cards of the same element";

		_sprite.Texture = GD.Load<Texture2D>("res://assets/nodes/combine.png");
		_scene = GD.Load<PackedScene>("res://prefabs/picker.tscn");
	}
	
	public void LoadCoreDefence()
	{
		_title = "Core Defence"; //needs lang lookup
		_tooltip = "Defend your core from enemies on a random map";

		_sprite.Texture = GD.Load<Texture2D>("res://assets/nodes/shield_up.png");
		_scene = GD.Load<PackedScene>("res://prefabs/CoreDef.tscn");
	}

	public void LoadEnergyGain()
	{
		_title = "Energy Gain"; //needs lang lookup
		_tooltip = "Permantly gain 1 Energy Regen of your choice";

		_sprite.Texture = GD.Load<Texture2D>("res://assets/nodes/multi.png");
		_scene = GD.Load<PackedScene>("res://prefabs/energyGain.tscn");
	}

	public void LoadCardChoice()
	{
		_title = "Card Gain"; //needs lang lookup
		_tooltip = "Choose one of 3 cards";

		_sprite.Texture = GD.Load<Texture2D>("res://assets/nodes/card.png");
		_scene = GD.Load<PackedScene>("res://prefabs/cardchoice.tscn");
	}

	public void InstantiateSceneFromNode()
	{
		GD.Print("Attempting to instantiate");
		_overworld.InScene = true;
		_visisted = true;
		_visitable = false;
		_sprite.SelfModulate = new Color(0.8f, 0.8f, 1f, 0.2f);
		_overworld.roundNum += 1;
		switch (_type)
		{
			case Type.CoreDefence:
				CoreDef defNode = _scene.Instantiate() as CoreDef;
				_overworld.AddChild(defNode);
				defNode.Setup();
				break;
			case Type.EnergyGain:
				GD.Print("Energy Node Instantiating");
				EnergyGain gainNode = _scene.Instantiate() as EnergyGain;
				_overworld.AddChild(gainNode);
				break;
			case Type.CardGain:
				GD.Print("Card Choice Node Instantiating");
				Cardchoice cardNode = _scene.Instantiate() as Cardchoice;
				_overworld.AddChild(cardNode);
				break;
			case Type.CardCombine:
				GD.Print("Card Combiner Node Instantiating");
				CardPicker picker = _scene.Instantiate() as CardPicker;
				_overworld.AddChild(picker);
				break;
		}
		
	}
}