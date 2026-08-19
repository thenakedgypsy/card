using Godot;
using System;

public partial class PlayerCore : Node2D, IHealth
{

	[Export]
	public float Health;
	public float CurrentHealth;
	private Sprite2D _sprite;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_sprite = GetNode<Sprite2D>("Sprite2D");
		CurrentHealth = Health;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public float GetCurrentHealth()
	{
		return CurrentHealth;
	}

	public float GetMaxHealth()
	{
		return Health;
	}

	public void TakeDamage(float value)
	{
		CurrentHealth -= value;
	    GD.Print($"Summon takes {value} damage");

		PackedScene scene = GD.Load<PackedScene>("res://prefabs/floating_damage_number.tscn");
		FloatingDamageNumber fdn = scene.Instantiate() as FloatingDamageNumber;
        AddChild(fdn);
        fdn.Appear((int)value, Card.Element.Fire);
		FlashRed();
	
	    if (CurrentHealth <= 0)
	    {
	        GD.Print("IS DESTROYED");
	
			CoreDef def = GetTree().GetFirstNodeInGroup("ActiveDef") as CoreDef;
			Overworld _overworld = GetTree().GetFirstNodeInGroup("Overworld") as Overworld;
			_overworld.InScene = false;
			def.QueueFree();
			
	        // Prevent double-death logic
	        SetProcess(false);
	        SetPhysicsProcess(false);
	
	        // Optional: stop collisions if you have them
	        SetDeferred("monitoring", false);
	
	        // Safely remove from tree at end of frame
	        CallDeferred(Node.MethodName.QueueFree);

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
}
