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
	private NavigationObstacle2D _obstacle;
	private HealthBar _healthBar;
	private Line2D _line;

	private int _drawLineRequestId = 0;

	[Export(PropertyHint.Range, "0.0,1.0")]
	public float NavigationSectionStart = 0.4f;

	[Export(PropertyHint.Range, "0.0,1.0")]
	public float NavigationSectionEnd = 0.6f;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_sprite = GetNode<Sprite2D>("Sprite2D");
		_obstacle = GetNode<NavigationObstacle2D>("NavigationObstacle2D");
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

		Color color;
		if (Element == Card.Element.Fire)
			color = Colors.Red;
		else if (Element == Card.Element.Water)
			color = Colors.Blue;
		else if (Element == Card.Element.Earth)
			color = Colors.Green;
		else if (Element == Card.Element.Wind)
			color = Colors.LightBlue;
		else
			color = Colors.Gray;

		_line.Points = new Vector2[] { Vector2.Zero, target - (GlobalPosition - new Vector2(0, 50)) };
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
		AttackRange = data["range"].ToString().ToFloat();
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
    	Vector2 barSize = _healthBar.Size * _healthBar.Scale;

    	float height = spriteSize.Y;

    	//
    	// Health bar
    	//
    	const float padding = 4f;
    	_healthBar.Position = new Vector2(-barSize.X * 0.5f, -(height * 0.5f) - padding - barSize.Y);

    	//
    	// Navigation obstacle
    	//
		float radius = Mathf.Max(spriteSize.X, spriteSize.Y) * 0.45f;
		//_obstacle.Radius = radius;
		UpdateNavigationObstacle();
	}

	private void UpdateNavigationObstacle()
	{
	    if (_sprite.Texture == null)
	        return;

	    Image image = _sprite.Texture.GetImage();

	    List<Vector2> points = new();

	    const int step = 2;

	    int startY = (int)(image.GetHeight() * NavigationSectionStart);
	    int endY = (int)(image.GetHeight() * NavigationSectionEnd);

	    for (int y = startY; y < endY; y += step)
	    {
	        for (int x = 0; x < image.GetWidth(); x += step)
	        {
	            if (image.GetPixel(x, y).A > 0.1f)
	                points.Add(new Vector2(x, y));
	        }
	    }

	    if (points.Count < 3)
	        return;

	    Vector2[] hull = Geometry2D.ConvexHull(points.ToArray());

	    Vector2 offset = new Vector2(
	        image.GetSize().X / 2.0f,
	        image.GetSize().Y / 2.0f
	    );

	    for (int i = 0; i < hull.Length; i++)
	    {
	        hull[i] = (hull[i] - offset) * _sprite.Scale;
	    }

	    _obstacle.Vertices = hull;
	}
}
