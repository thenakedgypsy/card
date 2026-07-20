using Godot;
using System;
public interface IHealth
{
	float GetMaxHealth();
	float GetCurrentHealth();
}

public partial class HealthBar : TextureProgressBar
{
	// The object this bar is displaying
	private IHealth healthTarget;

	// Assign any class implementing IHealth

	public override void _Ready()
	{
		if (GetParent() is IHealth health)
		{
			healthTarget = health;
			
			MaxValue = healthTarget.GetMaxHealth();
			Value = healthTarget.GetCurrentHealth();
		}
		else
		{
			GD.PrintErr("HealthBar parent does not implement IHealth");
		}
		Visible = false;
	}

	public override void _Process(double delta)
	{
		if (healthTarget == null)
			return;

		MaxValue = healthTarget.GetMaxHealth();
		Value = healthTarget.GetCurrentHealth();

		if (Value < MaxValue && !Visible)
		{
			Visible = true;
		}
	}
}
