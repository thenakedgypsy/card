using Godot;
using System;

public partial class Mouse : Node2D
{
	public bool IsDragging;
	public bool IsOverACard;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		GlobalPosition = GetGlobalMousePosition();
	}
}
