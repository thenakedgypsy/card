using Godot;
using System;

public partial class Card : Node2D
{
    public bool MouseIsOver;
    public bool IsDragging;
    public bool IsScaledUp;
    public bool ShouldReturnToHand = true;
    public bool IsInPlayzone;
    public bool WillPlay;
    public bool InHand;

    private Vector2 DragOffset;
    private bool _isBeingRemoved;

    public Hand hand; // IMPORTANT reference
	public Discard discard;

    public override void _Ready()
    {
        ZIndex = 4;
		discard = GetTree().GetFirstNodeInGroup("Discard") as Discard;
		hand = GetTree().GetFirstNodeInGroup("Hand") as Hand;
    }

    // =========================
    // DRAG SYSTEM
    // =========================

    public void StartDrag()
    {
        IsDragging = true;
        DragOffset = GlobalPosition - GetGlobalMousePosition();

        if (!IsScaledUp)
            ScaleUp();
    }

    public void UpdateDrag(Vector2 mousePos)
    {
        if (!IsDragging) return;

        GlobalPosition = mousePos + DragOffset;
		Rotation = 0;
    }

    public void EndDrag()
    {
        IsDragging = false;

        if (!MouseIsOver && IsScaledUp)
            ScaleDown();

        if (IsInPlayzone)
        {
            Play();
        }
    }

    // =========================
    // GAME LOGIC
    // =========================

    public void Play()
    {
        GD.Print($"{Name} played");

        WillPlay = false;
        ShouldReturnToHand = false;
        hand.QueueRemoveCard(this);

		Discard();
    }

	public void Discard()
	{
		GD.Print($"{Name} moved to discard");
		discard.AddCard(this);
	}

	public void AddToHand()
	{
		GD.Print($"{Name} moved to hand");
		ShouldReturnToHand = true;
		hand.AddCard(this);
	}

    public void Remove()
    {
        if (_isBeingRemoved) return;
        _isBeingRemoved = true;

		GD.Print($"{Name} removed from existance");
        QueueFree();
    }

    // =========================
    // MOUSE VISUALS
    // =========================

    public void MouseOver()
    {
        MouseIsOver = true;

        if (!IsScaledUp && !IsDragging)
            ScaleUp();
    }

    public void MouseOff()
    {
        MouseIsOver = false;

        if (!IsDragging && IsScaledUp)
            ScaleDown();
    }

    public void ScaleUp()
    {
        if (IsScaledUp) return;

        Scale *= 1.2f;
        Position -= new Vector2(0f, 50f);
        ZIndex = 1000;

        IsScaledUp = true;
    }

    public void ScaleDown()
    {
        if (!IsScaledUp) return;

        Scale /= 1.2f;
        Position += new Vector2(0f, 50f);
        ZIndex = 4;

        IsScaledUp = false;
    }
}