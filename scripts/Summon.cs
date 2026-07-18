using Godot;
using System;
using System.Collections.Generic;

public partial class Summon : Node2D, IHealth
{
	public int Health;
	public int CurrentHealth;
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

	public void Generate(Card.Element ele, Dictionary<string, Variant> data, string summonID)
	{
		Element = ele;
		Health = data["health"].ToString().ToInt();
		CurrentHealth = Health;
		Damage = data["damage"].ToString().ToInt();

        string path = $"res://assets/summons/{summonID}.png";

        Texture2D texture = GD.Load<Texture2D>(path);

		_sprite.Texture = texture;
	}

	public void TakeDamage(int value)
	{
	    CurrentHealth -= value;
	    GD.Print($"Summon takes {value} damage");

		FlashRed();
	
	    if (CurrentHealth <= 0)
	    {
	        GD.Print("IS DESTROYED");
	
	        // Prevent double-death logic
	        SetProcess(false);
	        SetPhysicsProcess(false);
	
	        // Optional: stop collisions if you have them
	        SetDeferred("monitoring", false);
	
	        // Safely remove from tree at end of frame
	        CallDeferred(Node.MethodName.QueueFree);
	    }
	}

	public float GetMaxHealth()
	{
		return Health;
	}

	public float GetCurrentHealth()
	{
		return CurrentHealth;
	}
}
