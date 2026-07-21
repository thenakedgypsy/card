using Godot;
using System;
using System.Collections.Generic;

public partial class Summon : Node2D, IHealth
{
	[Signal]
	public delegate void TurnFinishedEventHandler(Summon summon);

	[Export] public bool AttacksEnemies = false;
	[Export] public int AttackDamage = 1;
	[Export] public int AttackRange = 2; // tiles

	public int Health;
	public int CurrentHealth;
	public int Damage;
	public Card.Element Element;
	private Sprite2D _sprite;
	private HealthBar _healthBar;
	private Line2D _line;

	private int _drawLineRequestId = 0;
	private TurnManager _turnManager;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_turnManager = GetTree().GetFirstNodeInGroup("TurnManager") as TurnManager;
		_sprite = GetNode<Sprite2D>("Sprite2D");
    	_healthBar = GetNode<HealthBar>("HealthBar");
		_line = GetNodeOrNull<Line2D>("Line2D");
		if (_line == null)
		{
			_line = new Line2D();
			_line.Name = "Line2D";
			_line.ZIndex = 100;
			_line.Width = 2f;
			_line.DefaultColor = Colors.White;
			_line.Points = new Vector2[] { Vector2.Zero, Vector2.Zero };
			_line.Visible = false;
			AddChild(_line);
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public async void DrawLineBetween(Vector2 target, float width = 2f)
	{
	    if (_line == null)
	        return;

	    // Cleaned up color selection using a switch expression
	    Color color = Element switch
	    {
	        Card.Element.Fire => Colors.Red,
	        Card.Element.Water => Colors.Blue,
	        Card.Element.Earth => Colors.Green,
	        Card.Element.Wind => Colors.LightBlue,
	        _ => Colors.Gray
	    };

	    // 1. Calculate top of sprite in global coordinates (including scale)
	    float spriteHeight = (_sprite.Texture?.GetHeight() ?? 0f) * _sprite.Scale.Y;
	    Vector2 spriteTop = _sprite.GlobalPosition + new Vector2(0, -(spriteHeight - 32));

	    // 2. Convert global positions to local positions relative to this node
	    Vector2 localStart = spriteTop - GlobalPosition;
	    Vector2 localEnd = target - GlobalPosition;

	    _line.Points = new Vector2[] { localStart, localEnd };
	    _line.Width = width;
	    _line.DefaultColor = color;
	    _line.ZIndex = 100;
	    _line.Visible = true;

	    int requestId = ++_drawLineRequestId;
	    await ToSignal(GetTree().CreateTimer(0.15f), SceneTreeTimer.SignalName.Timeout);
	    if (requestId == _drawLineRequestId)
	    {
	        ClearDrawLine();
	    }
	}

	public void ClearDrawLine()
	{
		if (_line == null)
			return;

		_line.Visible = false;
	}

	public void TakeTurn()
	{
		if (!AttacksEnemies)
		{
			EndTurn();
			return;
		}

		Enemy nearestEnemy = null;
		int minDistance = int.MaxValue;

		Vector2I myCell = TurnManager.Instance.WorldToCell(GlobalPosition);

		var enemies = GetTree().GetNodesInGroup("Enemies");
		foreach (Node node in enemies)
		{
			if (node is Enemy enemy && GodotObject.IsInstanceValid(enemy))
			{
				Vector2I enemyCell = TurnManager.Instance.WorldToCell(enemy.GlobalPosition);
				int dist = TurnManager.Instance.TileDistance(myCell, enemyCell);

				if (dist < minDistance)
				{
					minDistance = dist;
					nearestEnemy = enemy;
				}
			}
		}

		if (nearestEnemy != null && minDistance <= AttackRange)
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
		if (enemy.HasMethod("TakeDamage"))
            enemy.Call("TakeDamage", AttackDamage);
		FlashRed();
		DrawLineBetween(enemy.GlobalPosition, 5f);
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
		AttackRange = data["range"].ToString().ToInt();
		AttacksEnemies = data["attacksEnemies"].ToString() == "true";
		Name = summonID;	

        string path = $"res://assets/summons/{summonID}.png";

        Texture2D texture = GD.Load<Texture2D>(path);

		_sprite.Texture = texture;

		UpdateVisualBounds();
	}

	public void TakeDamage(int value)
	{
	    CurrentHealth -= value;
	    GD.Print($"Summon takes {value} damage");

		FlashRed();
	
	    if (CurrentHealth <= 0)
	    {
	        GD.Print("IS DESTROYED");
			RemoveFromGroup("Summons");
			_turnManager.ClearCell(GlobalPosition);
			
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

	private void UpdateVisualBounds()
	{
	    if (_sprite.Texture == null)
	        return;
	
	    // Actual displayed size
	    Vector2 spriteSize = _sprite.Texture.GetSize() * _sprite.Scale;
	
	    // 1. Calculate correct offset so the bottom 16px sit below origin
	    if (_sprite.Centered)
	    {
	        _sprite.Offset = new Vector2(0, 16f - (spriteSize.Y * 0.5f));
	    }
	    else
	    {
	        _sprite.Offset = new Vector2(0, 16f - spriteSize.Y);
	    }
	
	    // 2. Position HealthBar dynamically relative to top of the sprite
	    if (_healthBar != null)
	    {
	        Vector2 barSize = _healthBar.Size * _healthBar.Scale;
	        const float padding = 4f;
	
	        // Find the top edge of the sprite relative to Node2D origin
	        float topOfSprite = _sprite.Centered 
	            ? _sprite.Offset.Y - (spriteSize.Y * 0.5f) 
	            : _sprite.Offset.Y;
	
	        _healthBar.Position = new Vector2(-barSize.X * 0.5f, topOfSprite - padding - barSize.Y);
	    }
	}
}