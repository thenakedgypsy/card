using Godot;
using System;
using System.Collections.Generic;


public partial class OverworldNode : Node2D // will be a node in the overworld tree, 
{
	public enum Type
	{
		Choice,
		CoreDefence,
		CardGain,
		EnergyGain
	}
	private bool _visitable;
	private bool _visisted;
	private Type _type;
	private string _title;
	private string _tooltip;
	private Sprite2D _sprite;
	private PackedScene _scene;
	private Dictionary<string, Variant> _sceneData;
	private Overworld _overworld;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_sprite = GetNode<Sprite2D>("Sprite2D");
		_overworld = GetParent<Overworld>();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void buildNode(Type type, Dictionary<string, Variant> data = null)
	{		//switch for types here pass the nested data on. 
		switch (type)
		{
			case Type.CoreDefence:
				LoadCoreDefence();
				break;
		}
	}

	public Dictionary<string, Variant> ExtractSceneData(Dictionary<string, Variant> data)
	{
		var sceneVariant = data["sceneData"];
		var godotDict = sceneVariant.AsGodotDictionary();
		Dictionary<string, Variant> SceneData = new Dictionary<string, Variant>();
		foreach (var key in godotDict.Keys)
		{
			string name = key.ToString();
			var valueVar = godotDict[key];
			SceneData.Add(name, valueVar);
		}

		return SceneData;
	}
	
	public void LoadCoreDefence()
	{
		_title = "Core Defence"; //needs lang lookup
		_tooltip = "Defend your core from enemies on a random map";
		_sprite.Texture = GD.Load<Texture2D>("res://assets/nodes/shield_up.png");
		_scene = GD.Load<PackedScene>("res://prefabs/CoreDef.tscn");

	}

	public void InstantiateSceneFromNode(Type type)
	{
		switch (type)
		{
			case Type.CoreDefence:
				CoreDef defNode = _scene.Instantiate() as CoreDef;
				_overworld.AddChild(defNode);
				defNode.Setup();
				_visisted = true;
				_visitable = false;
				break;
		}
		
	}
}
