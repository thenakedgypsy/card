using Godot;
using System;
using System.Collections.Generic;

public partial class Summon : Node2D
{
	public int Health;
	private int _currentHealth;
	public int Damage;
	public Card.Element Element;
	private Sprite2D _sprite;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_sprite = GetNode<Sprite2D>("Sprite2D");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void Generate(Card.Element ele, Dictionary<string, Variant> data, string summonID)
	{
		Element = ele;
		Health = data["health"].ToString().ToInt();
		_currentHealth = Health;
		Damage = data["damage"].ToString().ToInt();

        string path = $"res://assets/summons/{summonID}.png";

        Texture2D texture = GD.Load<Texture2D>(path);

		_sprite.Texture = texture;
	}

	public void TakeDamage(int value)
	{
		_currentHealth -= value;
		GD.Print($"Summon takes {value} damage");

		if (_currentHealth <= 0)
		{
			GD.Print("IS DESTROYED");
			QueueFree();
		}
	}
}
