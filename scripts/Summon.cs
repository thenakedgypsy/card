using Godot;
using System;
using System.Collections.Generic;

public partial class Summon : Node2D, IHealth
{
	[Signal]
	public delegate void TurnFinishedEventHandler(Summon summon);

	[Export] public bool AttacksEnemies = false;
	[Export] public int AttackDamage = 1;
	[Export] public float AttackRange = 100f;

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

	public void TakeTurn()
	{
		if (!AttacksEnemies)
		{
			EndTurn();
			return;
		}

		Enemy nearestEnemy = null;
		float minDistance = float.MaxValue;

		var enemies = GetTree().GetNodesInGroup("Enemies");
		foreach (Node node in enemies)
		{
			if (node is Enemy enemy && GodotObject.IsInstanceValid(enemy))
			{
				float dist = GlobalPosition.DistanceTo(enemy.GlobalPosition);
				if (dist < minDistance)
				{
					minDistance = dist;
					nearestEnemy = enemy;
				}
			}
		}

		if (nearestEnemy != null && GlobalPosition.DistanceTo(nearestEnemy.GlobalPosition) <= AttackRange)
		{
			Attack(nearestEnemy);
		}

		EndTurn();
	}

	private void Attack(Enemy enemy)
	{
		if (!GodotObject.IsInstanceValid(enemy))
			return;

		GD.Print($"[{Name}] ATTACK → '{enemy.Name}'");
		// TODO: implement summon attack effects and damage here.
		FlashRed();
	}

	private void EndTurn()
	{
		EmitSignal(SignalName.TurnFinished, this);
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
		AttackDamage = data["damage"].ToString().ToInt();
		AttackRange = data["range"].ToString().ToFloat();
		AttacksEnemies = data["attacksEnemies"].ToString() == "true";

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
