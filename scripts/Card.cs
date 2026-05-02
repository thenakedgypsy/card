using Godot;
using System;

public partial class Card : Node2D
{
     public enum CardType
    {
        Energy,
        Summon,
        Spell,
        Enchant
    }
    public enum Location
    {
        Deck,
        Hand,
        Discard,
        Exile,
        Unpurchased
    }


    public bool isDragging;
    public Location location;
    public CardType type;


    private bool _mouseIsOver;
    private bool _isScaledUp;
    private bool _shouldReturnToHand;

    private bool _isInPlayzone;
    private bool _willPlay;
    private Vector2 _dragOffset;
    private bool _isBeingRemoved;
    private Hand _hand; // IMPORTANT reference
	private Discard _discard;

    public override void _Ready()
    {
        ZIndex = 4;
		_discard = GetTree().GetFirstNodeInGroup("Discard") as Discard;
		_hand = GetTree().GetFirstNodeInGroup("Hand") as Hand;

        _shouldReturnToHand = true; //temp
        location = Location.Hand; //temp
    }

    // =========================
    // DRAG SYSTEM
    // =========================

    public void StartDrag()
    {
        isDragging = true;
        _dragOffset = GlobalPosition - GetGlobalMousePosition();

        if (!_isScaledUp)
            ScaleUp();
    }

    public void UpdateDrag(Vector2 mousePos)
    {
        if (!isDragging) return;

        GlobalPosition = mousePos + _dragOffset;
		Rotation = 0;
    }

    public void EndDrag()
    {
        isDragging = false;

        if (!_mouseIsOver && _isScaledUp)
            ScaleDown();

        if (_isInPlayzone)
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

        _willPlay = false;
        _shouldReturnToHand = false;
        _hand.QueueRemoveCard(this);

		Discard();
    }

	public void Discard()
	{
		GD.Print($"{Name} moved to _discard");
		_discard.AddCard(this);
        location = Location.Discard;
	}

	public void AddToHand()
	{
		GD.Print($"{Name} moved to _hand");
		_shouldReturnToHand = true;
		_hand.AddCard(this);
        location = Location.Hand;
	}

    public void Exile()
    {
        location = Location.Exile;
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
        _mouseIsOver = true;

        if (!_isScaledUp && !isDragging)
            ScaleUp();
    }

    public void MouseOff()
    {
        _mouseIsOver = false;

        if (!isDragging && _isScaledUp)
            ScaleDown();
    }

    public void ScaleUp()
    {
        if (_isScaledUp) return;

        Scale *= 1.2f;
        Position -= new Vector2(0f, 50f);
        ZIndex = 1000;

        _isScaledUp = true;
    }

    public void ScaleDown()
    {
        if (!_isScaledUp) return;

        Scale /= 1.2f;
        Position += new Vector2(0f, 50f);
        ZIndex = 4;

        _isScaledUp = false;
    }

    public void EnterPlayZone()
    {
        _isInPlayzone = true;
    }

    public void ExitPlayZone()
    {
        _isInPlayzone = false;
    }
}