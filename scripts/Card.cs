using Godot;
using System;

public partial class Card : Node2D
{

	public bool MouseIsOver;
	public bool IsDragging;
	private Vector2 DragOffset;
	private Effect[] Effects;
	public bool IsScaledUp;
	private Mouse mouse;
	public bool ShouldReturnToHand;
	public bool IsInPlayzone;
	public bool WillPlay;
	public bool InHand;
	private Hand hand;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		mouse = GetTree().GetFirstNodeInGroup("Mouse") as Mouse;
		hand = GetTree().GetFirstNodeInGroup("Hand") as Hand;
		// atm this is just true at start
		InHand = true;
		
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		HandleInteraction();
		if (WillPlay && !InHand)
		{
			Play();
		}
	}

	public void ApplyEffects()
	{

	}

	public void Play()
	{
		ApplyEffects();
		Discard();
		hand.UpdateHand();
	}

	public void Discard()
	{
		Visible = false;
		Position = new Vector2(-2000, -2000);
		hand.UpdateHand();
	}
	public void HandleInteraction()
	{
		if (MouseIsOver && IsScaledUp || IsDragging )
		{
			CheckDragStatus();
		}
		if (MouseIsOver)
		{
			MouseOver();
		}
		if (!MouseIsOver && !IsDragging && IsScaledUp)
		{
			ScaleDown();
		}
	}

	public void MouseOver()
	{
		MouseIsOver = true;
		//GD.Print("Mousing over card");
		if (!mouse.IsDragging && !mouse.IsOverACard)
		{			
			mouse.IsOverACard = true;
			ScaleUp();
		}
		hand.UpdateHand();
	}

	public void MouseOff()
	{
		//GD.Print("Mouse off card");
		if (!IsDragging)
		{
			MouseIsOver = false;
			
			if (IsScaledUp)
			{
				mouse.IsOverACard = false;
				ScaleDown();
			}
		}
		hand.UpdateHand();
	}

	public void ScaleUp()
	{
		if (MouseIsOver || IsDragging)
		{
			Scale = Scale * 1.2f;
			Position -= new Vector2(0f, 50f);
			IsScaledUp = true;
			hand.UpdateHand();
		}
	}

	public void ScaleDown()
	{
		Scale = Scale / 1.2f;
		Position += new Vector2(0f, 50f);
		IsScaledUp = false;
		hand.UpdateHand();
	}

	public void CheckDragStatus()
	{
		if (Input.IsActionPressed("lClick"))
		{
			if (!IsDragging)
			{
				DragOffset = GlobalPosition - GetGlobalMousePosition();
			}

			Drag();
		}
		else if (IsDragging)
		{
			mouse.IsDragging = false;
			HandleDropDelay();
		}
		
	}

	private async void HandleDropDelay()
	{
	    await ToSignal(GetTree().CreateTimer(0.1f), "timeout"); // 100ms delay
		if (!Input.IsActionPressed("lClick"))
		{
			Drop();
		}
	}

	private void Drop()
	{	
		IsDragging = false;
		if (IsInPlayzone)
		{
			WillPlay = true;
			hand.UpdateHand();
		}
		else
		{
			ShouldReturnToHand = true;
			hand.UpdateHand();
		}
		
	}

	public void Drag()
	{
		GlobalPosition = GetGlobalMousePosition() + DragOffset;
		Rotation = 0;

		if (!IsDragging)
		{
			mouse.IsDragging = true;
			IsDragging = true;
		}
	}

	private void _instantiateArt()
	{
		
	}

	private void _instantiateText()
	{
		
	}

}
